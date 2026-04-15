using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A* Pathfinding that builds its occupancy grid from live LiDAR raycast data.
/// The grid is updated every time UpdateGridWithLidarData() is called so the
/// planner always reflects the drone's latest scan of the environment.
/// </summary>
public class AStarPathfinder : MonoBehaviour
{
    // ------------------------------------------------------------------ grid
    [Header("Grid Settings")]
    public float gridCellSize   = 0.5f;
    public Vector3 gridCenter   = Vector3.zero;
    public Vector3 gridSize     = new Vector3(20f, 10f, 20f);

    // ---------------------------------------------------------- pathfinding
    [Header("Pathfinding Settings")]
    public float diagonalMovementCost = 1.414f;
    public float straightMovementCost = 1f;
    /// <summary>
    /// Extra cost multiplier added to cells whose nearest obstacle is within
    /// <see cref="obstacleProximityRadius"/> metres.  Higher → path stays
    /// further from walls even if slightly longer.
    /// </summary>
    public float proximityPenalty         = 3f;
    public float obstacleProximityRadius  = 1.5f;

    // --------------------------------------------------------------- fields
    private Dictionary<Vector3Int, Node> _grid = new Dictionary<Vector3Int, Node>();
    private Vector3Int _gridDimensions;

    // ================================================================ Node
    public class Node : System.IComparable<Node>
    {
        public Vector3Int gridPos;
        public Vector3    worldPos;
        public float gCost;
        public float hCost;
        public float fCost;
        public Node  parent;
        public bool  walkable            = true;
        public float nearestObstacleDist = float.MaxValue;
        public int heapIndex;

        public Node(Vector3Int pos, Vector3 world)
        {
            gridPos  = pos;
            worldPos = world;
        }

        public int CompareTo(Node other)
        {
            int compare = fCost.CompareTo(other.fCost);
            if (compare == 0) compare = hCost.CompareTo(other.hCost);
            return -compare;
        }
    }

    public class Heap<T> where T : System.IComparable<T>
    {
        T[] items;
        int currentItemCount;

        public Heap(int maxHeapSize) { items = new T[maxHeapSize]; }

        public void Add(T item)
        {
            if (item is Node n) n.heapIndex = currentItemCount;
            items[currentItemCount] = item;
            SortUp(item);
            currentItemCount++;
        }

        public T RemoveFirst()
        {
            T firstItem = items[0];
            currentItemCount--;
            items[0] = items[currentItemCount];
            if (items[0] is Node n) n.heapIndex = 0;
            SortDown(items[0]);
            return firstItem;
        }

        public void UpdateItem(T item) { SortUp(item); }

        public int Count { get { return currentItemCount; } }

        public bool Contains(T item)
        {
            int index = (item as Node).heapIndex;
            return index < currentItemCount && Equals(items[index], item);
        }

        void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = (item as Node).heapIndex * 2 + 1;
                int childIndexRight = (item as Node).heapIndex * 2 + 2;
                int swapIndex = 0;

                if (childIndexLeft < currentItemCount)
                {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < currentItemCount)
                    {
                        if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0)
                            swapIndex = childIndexRight;
                    }

                    if (item.CompareTo(items[swapIndex]) < 0)
                    {
                        Swap(item, items[swapIndex]);
                    }
                    else return;
                }
                else return;
            }
        }

        void SortUp(T item)
        {
            int parentIndex = ((item as Node).heapIndex - 1) / 2;

            while (true)
            {
                T parentItem = items[parentIndex];
                if (item.CompareTo(parentItem) > 0) Swap(item, parentItem);
                else break;
                parentIndex = ((item as Node).heapIndex - 1) / 2;
            }
        }

        void Swap(T itemA, T itemB)
        {
            items[(itemA as Node).heapIndex] = itemB;
            items[(itemB as Node).heapIndex] = itemA;
            int itemAIndex = (itemA as Node).heapIndex;
            (itemA as Node).heapIndex = (itemB as Node).heapIndex;
            (itemB as Node).heapIndex = itemAIndex;
        }
    }

    // ================================================================ Init
    void OnEnable() => InitializeGrid();

    void InitializeGrid()
    {
        _grid.Clear();

        _gridDimensions = new Vector3Int(
            Mathf.Max(1, Mathf.RoundToInt(gridSize.x / gridCellSize)),
            Mathf.Max(1, Mathf.RoundToInt(gridSize.y / gridCellSize)),
            Mathf.Max(1, Mathf.RoundToInt(gridSize.z / gridCellSize)));

        Vector3 gridStart = gridCenter - gridSize * 0.5f;
        for (int x = 0; x < _gridDimensions.x; x++)
        for (int y = 0; y < _gridDimensions.y; y++)
        for (int z = 0; z < _gridDimensions.z; z++)
        {
            var gp = new Vector3Int(x, y, z);
            var wp = gridStart + new Vector3(x, y, z) * gridCellSize
                               + Vector3.one * gridCellSize * 0.5f;
            _grid[gp] = new Node(gp, wp);
        }
    }

    // ================================================ LiDAR grid update
    /// <summary>
    /// Fires a sphere of rays from <paramref name="dronePosition"/> and marks
    /// any cell hit (plus a safety buffer) as non-walkable.  Also stores the
    /// distance to the nearest obstacle so the planner can penalise proximity.
    /// </summary>
    public void UpdateGridWithLidarData(Vector3 dronePosition,
                                        float   scanDistance,
                                        int     rayCount)
    {
        // Reset occupancy
        foreach (var n in _grid.Values)
        {
            n.walkable            = true;
            n.nearestObstacleDist = scanDistance;
        }

        // Distribute rays uniformly over a sphere (Fibonacci lattice)
        for (int i = 0; i < rayCount; i++)
        {
            float theta = Mathf.Acos(1f - 2f * (i + 0.5f) / rayCount);
            float phi   = Mathf.PI * (1f + Mathf.Sqrt(5f)) * i;

            Vector3 dir = new Vector3(
                Mathf.Sin(theta) * Mathf.Cos(phi),
                Mathf.Cos(theta),
                Mathf.Sin(theta) * Mathf.Sin(phi));

            if (Physics.Raycast(dronePosition, dir, out RaycastHit hit, scanDistance))
            {
                Vector3Int hitCell = WorldToGridPos(hit.point);
                int bufSize = Mathf.CeilToInt(obstacleProximityRadius / gridCellSize);

                for (int bx = -bufSize; bx <= bufSize; bx++)
                for (int by = -bufSize; by <= bufSize; by++)
                for (int bz = -bufSize; bz <= bufSize; bz++)
                {
                    var bp = hitCell + new Vector3Int(bx, by, bz);
                    if (!IsValidGridPos(bp)) continue;

                    Node n        = _grid[bp];
                    float dist    = Vector3.Distance(n.worldPos, hit.point);
                    bool isCore   = (bx == 0 && by == 0 && bz == 0);

                    if (isCore) n.walkable = false;

                    if (dist < n.nearestObstacleDist)
                        n.nearestObstacleDist = dist;
                }
            }
        }
    }

    // ================================================== A* FindPath
    /// <summary>
    /// Returns a list of world-space waypoints from <paramref name="startPos"/>
    /// to <paramref name="goalPos"/>.  Falls back to a direct two-point path
    /// when no route exists.
    /// </summary>
    public List<Vector3> FindPath(Vector3 startPos, Vector3 goalPos)
    {
        Vector3Int startGrid = WorldToGridPos(startPos);
        Vector3Int goalGrid  = WorldToGridPos(goalPos);

        // Clamp to valid range
        startGrid = ClampToGrid(startGrid);
        goalGrid  = ClampToGrid(goalGrid);

        if (!_grid[goalGrid].walkable)
            goalGrid = FindNearestWalkableCell(goalGrid);

        // Reset cost data only for nodes that will be touched
        // (full reset every call is acceptable for modest grid sizes)
        foreach (var n in _grid.Values)
        {
            n.gCost  = float.MaxValue;
            n.hCost  = 0f;
            n.fCost  = float.MaxValue;
            n.parent = null;
        }

        var openSet   = new Heap<Node>(_grid.Count);
        var closedSet = new HashSet<Vector3Int>();

        Node startNode = _grid[startGrid];
        Node goalNode  = _grid[goalGrid];

        startNode.gCost = 0f;
        startNode.hCost = Heuristic(startGrid, goalGrid);
        startNode.fCost = startNode.hCost;
        openSet.Add(startNode);

        int maxIterations = 3000;
        int iterations = 0;
        Node closestNode = startNode;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            // Pop lowest-fCost node
            Node current = openSet.RemoveFirst();

            if (current == goalNode)
                return ReconstructPath(goalNode);

            if (current.hCost < closestNode.hCost)
                closestNode = current;

            closedSet.Add(current.gridPos);

            foreach (Vector3Int nPos in GetNeighbors(current.gridPos))
            {
                if (closedSet.Contains(nPos)) continue;
                Node neighbor = _grid[nPos];
                if (!neighbor.walkable) continue;

                // Movement cost + proximity penalty
                float moveCost = Vector3Int.Distance(current.gridPos, nPos) > 1.1f
                    ? diagonalMovementCost : straightMovementCost;

                float proximityFactor = neighbor.nearestObstacleDist < obstacleProximityRadius
                    ? proximityPenalty * (1f - neighbor.nearestObstacleDist / obstacleProximityRadius)
                    : 0f;

                float tentativeG = current.gCost + moveCost + proximityFactor;

                if (tentativeG < neighbor.gCost)
                {
                    neighbor.gCost  = tentativeG;
                    neighbor.hCost  = Heuristic(nPos, goalGrid);
                    neighbor.fCost  = neighbor.gCost + neighbor.hCost;
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                    else
                        openSet.UpdateItem(neighbor);
                }
            }
        }

        if (iterations >= maxIterations)
            Debug.LogWarning($"[A*] Pathfinding hit max iterations ({maxIterations}). Returning partial path {startPos} → {goalPos}.");
        else
            Debug.LogWarning($"[A*] No path found {startPos} → {goalPos}. Using direct line.");
            
        return ReconstructPath(closestNode);
    }

    // =========================================== direction helper
    /// <summary>
    /// Returns the normalised direction from <paramref name="currentPos"/>
    /// toward the active waypoint on <paramref name="path"/>.
    /// </summary>
    public Vector3 GetPathDirection(Vector3 currentPos,
                                    List<Vector3> path,
                                    int waypointIndex)
    {
        if (path == null || path.Count == 0) return Vector3.zero;
        waypointIndex = Mathf.Clamp(waypointIndex, 0, path.Count - 1);
        return (path[waypointIndex] - currentPos).normalized;
    }

    // ================================================ helpers
    private List<Vector3> ReconstructPath(Node end)
    {
        var path    = new List<Vector3>();
        Node current = end;
        while (current != null)
        {
            path.Insert(0, current.worldPos);
            current = current.parent;
        }
        return path;
    }

    private float Heuristic(Vector3Int a, Vector3Int b)
    {
        float dx = Mathf.Abs(b.x - a.x);
        float dy = Mathf.Abs(b.y - a.y);
        float dz = Mathf.Abs(b.z - a.z);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private IEnumerable<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            if (x == 0 && y == 0 && z == 0) continue;
            var np = pos + new Vector3Int(x, y, z);
            if (IsValidGridPos(np)) yield return np;
        }
    }

    private Vector3Int FindNearestWalkableCell(Vector3Int target)
    {
        int maxR = Mathf.Max(_gridDimensions.x, _gridDimensions.y, _gridDimensions.z);
        for (int r = 1; r < maxR; r++)
        for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r; y++)
        for (int z = -r; z <= r; z++)
        {
            if (Mathf.Abs(x) < r && Mathf.Abs(y) < r && Mathf.Abs(z) < r) continue;
            var c = target + new Vector3Int(x, y, z);
            if (IsValidGridPos(c) && _grid[c].walkable) return c;
        }
        return target;
    }

    private Vector3Int WorldToGridPos(Vector3 worldPos)
    {
        Vector3 relative = worldPos - (gridCenter - gridSize * 0.5f);
        return Vector3Int.FloorToInt(relative / gridCellSize);
    }

    private Vector3Int ClampToGrid(Vector3Int p) =>
        new Vector3Int(
            Mathf.Clamp(p.x, 0, _gridDimensions.x - 1),
            Mathf.Clamp(p.y, 0, _gridDimensions.y - 1),
            Mathf.Clamp(p.z, 0, _gridDimensions.z - 1));

    private bool IsValidGridPos(Vector3Int p) =>
        p.x >= 0 && p.x < _gridDimensions.x &&
        p.y >= 0 && p.y < _gridDimensions.y &&
        p.z >= 0 && p.z < _gridDimensions.z;

    // ================================================ gizmos
    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(gridCenter, gridSize);
    }
}

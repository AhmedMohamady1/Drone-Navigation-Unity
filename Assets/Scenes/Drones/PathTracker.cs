using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Records every position the drone visits during an episode.
/// Also maintains a voxel occupancy map of explored cells so the
/// agent can observe *which parts of space it has already visited*.
/// Provides the neural network observation arrays consumed by DroneAgent.
/// </summary>
public class PathTracker : MonoBehaviour
{
    // --------------------------------------------------------- step record
    [System.Serializable]
    public class PathStep
    {
        public Vector3 position;
        public float   distanceFromStart;
        public float   timeSinceStart;
        public int     stepIndex;

        public PathStep(Vector3 pos, float dist, float time, int idx)
        {
            position          = pos;
            distanceFromStart = dist;
            timeSinceStart    = time;
            stepIndex         = idx;
        }
    }

    // ---------------------------------------------------- private state
    private List<PathStep> _recordedPath  = new List<PathStep>();
    private Vector3        _startPos;
    private float          _startTime;
    private int            _stepCount;
    private bool           _tracking;

    private Vector3        _targetPos;
    private int            _attempts;
    private List<Vector3>  _plannedPath      = new List<Vector3>();
    private int            _waypointIdx;

    /// <summary>Voxel size used for the explored-cells occupancy map.</summary>
    [Tooltip("Voxel resolution of the explored-cell map (metres).")]
    public float exploredVoxelSize = 1f;

    // key = voxel coord, value = visit count
    private Dictionary<Vector3Int, int> _exploredCells = new Dictionary<Vector3Int, int>();

    // ================================================================ API
    public void StartTracking(Vector3 startPos, Vector3 target)
    {
        _recordedPath.Clear();
        _exploredCells.Clear();
        _stepCount    = 0;
        _startPos     = startPos;
        _startTime    = Time.time;
        _targetPos    = target;
        _attempts     = 0;
        _tracking     = true;
        _waypointIdx  = 0;
        RecordStep(startPos);
    }

    public void StopTracking() => _tracking = false;

    public void RecordStep(Vector3 pos)
    {
        if (!_tracking) return;

        float dist = Vector3.Distance(_startPos, pos);
        float time = Time.time - _startTime;

        _recordedPath.Add(new PathStep(pos, dist, time, _stepCount));
        _stepCount++;

        // Mark voxel as explored
        Vector3Int voxel = WorldToVoxel(pos);
        if (_exploredCells.ContainsKey(voxel))
            _exploredCells[voxel]++;
        else
            _exploredCells[voxel] = 1;
    }

    public void SetPlannedPath(List<Vector3> path)
    {
        _plannedPath = new List<Vector3>(path);
        _waypointIdx = 0;
    }

    public void UpdateWaypointProgress(Vector3 pos)
    {
        if (_plannedPath == null || _plannedPath.Count == 0) return;

        float minDist  = float.MaxValue;
        int   nearIdx  = _waypointIdx;
        for (int i = _waypointIdx; i < _plannedPath.Count; i++)
        {
            float d = Vector3.Distance(pos, _plannedPath[i]);
            if (d < minDist) { minDist = d; nearIdx = i; }
        }
        _waypointIdx = Mathf.Min(nearIdx + 1, _plannedPath.Count - 1);
    }

    public void IncrementAttemptCount() => _attempts++;

    // ------------------------------------------------------ accessors
    public int            GetCurrentWaypointIndex() => _waypointIdx;
    public float          GetPathCompletion()  => _plannedPath.Count == 0 ? 0f
                                                  : (float)_waypointIdx / _plannedPath.Count;
    public List<PathStep> GetRecordedPath()    => new List<PathStep>(_recordedPath);
    public List<Vector3>  GetPlannedPath()     => new List<Vector3>(_plannedPath);
    public float          GetTotalPathLength() => _recordedPath.Count == 0 ? 0f
                                                  : _recordedPath[_recordedPath.Count - 1].distanceFromStart;
    public float          GetTotalTime()       => _recordedPath.Count == 0 ? 0f
                                                  : _recordedPath[_recordedPath.Count - 1].timeSinceStart;
    public int            GetStepCount()       => _stepCount;
    public int            GetAttempts()        => _attempts;
    public Vector3        GetTargetPosition()      => _targetPos;
    public Vector3        GetEpisodeStartPosition() => _startPos;
    public int            GetExploredCellCount()    => _exploredCells.Count;

    // ============================================= observation arrays

    /// <summary>
    /// Flattens the last <paramref name="maxSteps"/> recorded positions into a
    /// float array (x,y,z per step) normalised by a 20 m range.
    /// </summary>
    public float[] GetPathAsObservations(int maxSteps = 20)
    {
        float[] obs = new float[maxSteps * 3];
        int     end = _recordedPath.Count;
        int   start = Mathf.Max(0, end - maxSteps);   // use the most-recent steps

        int slot = 0;
        for (int i = start; i < end && slot < maxSteps; i++, slot++)
        {
            Vector3 norm = (_recordedPath[i].position - _startPos) / 20f;
            obs[slot * 3]     = norm.x;
            obs[slot * 3 + 1] = norm.y;
            obs[slot * 3 + 2] = norm.z;
        }
        // remaining slots already zero
        return obs;
    }

    /// <summary>
    /// Returns 10 normalised transform / progress values for the sensor.
    /// </summary>
    public float[] GetTransformAsObservations()
    {
        float[] obs = new float[10];
        if (_recordedPath.Count == 0) return obs;

        Vector3 cur = _recordedPath[_recordedPath.Count - 1].position;

        obs[0] = cur.x / 20f;
        obs[1] = cur.y / 20f;
        obs[2] = cur.z / 20f;
        obs[3] = Vector3.Distance(cur, _targetPos) / 50f;
        obs[4] = GetPathCompletion();
        obs[5] = Mathf.Min(_stepCount / 1000f, 1f);
        obs[6] = Mathf.Min(GetTotalTime() / 60f, 1f);
        obs[7] = GetTotalPathLength() / 100f;
        obs[8] = Mathf.Min(_attempts / 10f, 1f);
        obs[9] = _waypointIdx / Mathf.Max(1f, _plannedPath.Count);

        return obs;
    }

    /// <summary>
    /// Returns a flat array indicating whether nearby voxels have been
    /// visited.  Samples a <paramref name="halfExtent"/>-unit cube around
    /// <paramref name="centre"/> at <see cref="exploredVoxelSize"/> resolution.
    /// Output length = (2*halfExtent/voxelSize + 1)^3, capped at
    /// <paramref name="maxObs"/>.
    /// </summary>
    public float[] GetExploredMapObservations(Vector3 centre,
                                               int halfExtentCells = 3,
                                               int maxObs = 125)
    {
        float[] obs  = new float[maxObs];
        int     idx  = 0;
        Vector3Int vc = WorldToVoxel(centre);

        for (int x = -halfExtentCells; x <= halfExtentCells && idx < maxObs; x++)
        for (int y = -halfExtentCells; y <= halfExtentCells && idx < maxObs; y++)
        for (int z = -halfExtentCells; z <= halfExtentCells && idx < maxObs; z++)
        {
            var key = vc + new Vector3Int(x, y, z);
            obs[idx++] = _exploredCells.ContainsKey(key) ? 1f : 0f;
        }
        return obs;
    }

    // --------------------------------------------------- helpers
    private Vector3Int WorldToVoxel(Vector3 p) =>
        new Vector3Int(
            Mathf.FloorToInt(p.x / exploredVoxelSize),
            Mathf.FloorToInt(p.y / exploredVoxelSize),
            Mathf.FloorToInt(p.z / exploredVoxelSize));
}

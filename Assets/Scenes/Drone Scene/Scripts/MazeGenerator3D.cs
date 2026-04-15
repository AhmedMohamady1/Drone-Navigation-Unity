using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator3D : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 11;  // Must be odd
    public int height = 11; // Must be odd
    public int depth = 11;  // Must be odd
    public float scale = 2.0f; // Size of each cube

    [Header("Materials")]
    public Material wallMaterial;

    private int[,,] maze;

    void Start()
    {
        GenerateMaze();
        DrawMaze();
    }

    void GenerateMaze()
    {
        maze = new int[width, height, depth];
        // Initialize everything as walls (0)
        
        Stack<Vector3Int> stack = new Stack<Vector3Int>();
        Vector3Int start = new Vector3Int(0, 0, 0);
        maze[start.x, start.y, start.z] = 1; // 1 is path
        stack.Push(start);

        Vector3Int[] directions = {
            new Vector3Int(0, 0, 2), new Vector3Int(0, 0, -2),
            new Vector3Int(0, 2, 0), new Vector3Int(0, -2, 0),
            new Vector3Int(2, 0, 0), new Vector3Int(-2, 0, 0)
        };

        while (stack.Count > 0)
        {
            Vector3Int current = stack.Peek();
            List<Vector3Int> neighbors = new List<Vector3Int>();

            foreach (var dir in directions)
            {
                Vector3Int next = current + dir;
                if (IsInBounds(next) && maze[next.x, next.y, next.z] == 0)
                {
                    neighbors.Add(next);
                }
            }

            if (neighbors.Count > 0)
            {
                Vector3Int chosen = neighbors[Random.Range(0, neighbors.Count)];
                // Remove wall between current and chosen
                Vector3Int wallPos = current + (chosen - current) / 2;
                maze[wallPos.x, wallPos.y, wallPos.z] = 1;
                maze[chosen.x, chosen.y, chosen.z] = 1;
                
                stack.Push(chosen);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    bool IsInBounds(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height && pos.z >= 0 && pos.z < depth;
    }

    void DrawMaze()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    // Draw a cube only if it's a wall (0)
                    if (maze[x, y, z] == 0)
                    {
                        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        wall.transform.position = new Vector3(x, y, z) * scale;
                        wall.transform.localScale = Vector3.one * scale;
                        wall.transform.parent = this.transform;
                        
                        if (wallMaterial != null)
                            wall.GetComponent<Renderer>().material = wallMaterial;

                        // Add a Rigidbody or Static tag for drone collisions
                        wall.isStatic = true; 
                    }
                }
            }
        }
    }
}
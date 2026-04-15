using UnityEngine;
using System.Collections.Generic;

public class Lidar3DScanner : MonoBehaviour
{
    [Header("Scanner Configuration")]
    public int raysPerFrame = 64; // 8x8 grid
    public float maxDistance = 50f;
    public float fieldOfView = 90f;
    public LayerMask detectionLayers = -1;

    [Header("Scanning Parameters")]
    public float scanSpeed = 90f; // degrees per second
    public float scanRange = 360f; // 360 for full rotation
    public bool continuousScanning = true;
    public bool autoStartScanning = true;

    [Header("Point Cloud Generation")]
    public float pointSize = 0.05f;
    public Material pointCloudMaterial;
    public Color pointColor = Color.cyan;
    public bool colorByDistance = true;

    [Header("Visualization")]
    public bool showScanningRays = true;
    public bool showRealTimePointCloud = true;

    // Scanner state
    private bool isScanning = false;
    private float currentScanAngle = 0f;
    private Camera scannerCamera;

    // Point cloud data
    private List<Vector3> pointCloudVertices;
    private List<Color> pointCloudColors;
    private List<Vector3> pointCloudNormals;

    // Mesh components
    private Mesh pointCloudMesh;
    private GameObject pointCloudObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Real-time scanning visualization
    private List<Vector3> framePoints;
    private float lastScanTime = 0f;

    // Public access
    public int PointCount => pointCloudVertices?.Count ?? 0;
    public bool IsScanning => isScanning;
    public float ScanProgress => currentScanAngle / scanRange;

    void Start()
    {
        InitializeScanner();
        InitializePointCloud();

        if (autoStartScanning)
        {
            StartScanning();
        }
    }

    void InitializeScanner()
    {
        // Setup camera
        scannerCamera = GetComponent<Camera>();
        if (scannerCamera == null)
        {
            scannerCamera = gameObject.AddComponent<Camera>();
        }

        scannerCamera.enabled = false;
        scannerCamera.fieldOfView = fieldOfView;

        // Initialize point cloud storage
        pointCloudVertices = new List<Vector3>();
        pointCloudColors = new List<Color>();
        pointCloudNormals = new List<Vector3>();
        framePoints = new List<Vector3>();
    }

    void InitializePointCloud()
    {
        // Create point cloud container
        pointCloudObject = new GameObject("3D_Scanner_PointCloud");
        pointCloudObject.transform.SetParent(transform.parent);
        pointCloudObject.transform.position = Vector3.zero;

        // Add mesh components
        meshFilter = pointCloudObject.AddComponent<MeshFilter>();
        meshRenderer = pointCloudObject.AddComponent<MeshRenderer>();

        // Create mesh
        pointCloudMesh = new Mesh();
        pointCloudMesh.name = "3D Scanner Point Cloud";
        pointCloudMesh.MarkDynamic(); // Optimize for frequent updates
        meshFilter.mesh = pointCloudMesh;

        // Setup material
        if (pointCloudMaterial != null)
        {
            meshRenderer.material = pointCloudMaterial;
        }
        else
        {
            // Create point cloud optimized material
            meshRenderer.material = CreatePointCloudMaterial();
        }
    }

    Material CreatePointCloudMaterial()
{
    // Try shaders in order depending on your render pipeline
    string[] shaderNames = new string[]
    {
        "Universal Render Pipeline/Lit",   // URP
        "HDRP/Lit",                        // HDRP
        "Standard",                        // Built-in
        "Sprites/Default",                 // Fallback
        "Unlit/Color"                      // Last resort
    };

    Shader foundShader = null;
    foreach (string shaderName in shaderNames)
    {
        foundShader = Shader.Find(shaderName);
        if (foundShader != null) break;
    }

    if (foundShader == null)
    {
        Debug.LogError("No valid shader found for LiDAR point cloud material!");
        return new Material(Shader.Find("Hidden/InternalErrorShader"));
    }

    Material mat = new Material(foundShader);
    mat.color = pointColor;
    return mat;
}

    void Update()
    {
        if (isScanning)
        {
            PerformScanning();
            UpdatePointCloudVisualization();
        }

        HandleInput();
    }

    void PerformScanning()
    {
        // Calculate how many degrees to scan this frame
        float scanDelta = scanSpeed * Time.deltaTime;

        // Perform scanning in segments
        int segments = Mathf.CeilToInt(scanDelta / (fieldOfView / raysPerFrame));
        segments = Mathf.Max(1, segments);

        for (int i = 0; i < segments; i++)
        {
            if (currentScanAngle >= scanRange && !continuousScanning)
            {
                StopScanning();
                break;
            }

            ScanSingleFrame();
            currentScanAngle += scanDelta / segments;

            // Wrap around for continuous scanning
            if (currentScanAngle >= scanRange && continuousScanning)
            {
                currentScanAngle = 0f;
            }
        }
    }

    void ScanSingleFrame()
    {
        framePoints.Clear();

        // Calculate grid dimensions (approximate square grid)
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(raysPerFrame));
        int actualRays = gridSize * gridSize;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Calculate ray direction for this grid point
                float screenX = (x + 0.5f) / gridSize;
                float screenY = (y + 0.5f) / gridSize;

                // Create ray
                Ray ray = scannerCamera.ViewportPointToRay(new Vector3(screenX, screenY, 0));
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, maxDistance, detectionLayers))
                {
                    // Transform point to world space (relative to scanner origin)
                    Vector3 worldPoint = hit.point;

                    // Add to point cloud
                    pointCloudVertices.Add(worldPoint);
                    pointCloudNormals.Add(hit.normal);

                    // Calculate color
                    Color pointColor = colorByDistance ?
                        GetDistanceColor(hit.distance) : this.pointColor;
                    pointCloudColors.Add(pointColor);

                    // Add to frame points for visualization
                    framePoints.Add(worldPoint);

                    // Debug visualization
                    if (showScanningRays)
                    {
                        Debug.DrawRay(transform.position, worldPoint - transform.position,
                                    Color.green, 0.1f);
                    }
                }
                else
                {
                    // Optional: Add points at max distance for empty space visualization
                    // Vector3 maxPoint = ray.origin + ray.direction * maxDistance;
                    // framePoints.Add(maxPoint);

                    if (showScanningRays)
                    {
                        Debug.DrawRay(transform.position, ray.direction * maxDistance,
                                    Color.red, 0.1f);
                    }
                }
            }
        }

        lastScanTime = Time.time;
    }

    Color GetDistanceColor(float distance)
    {
        float normalized = Mathf.Clamp01(distance / maxDistance);

        // Gradient from red (close) to blue (far)
        if (normalized < 0.5f)
        {
            return Color.Lerp(Color.red, Color.yellow, normalized * 2f);
        }
        else
        {
            return Color.Lerp(Color.yellow, Color.blue, (normalized - 0.5f) * 2f);
        }
    }

    void UpdatePointCloudVisualization()
    {
        if (pointCloudVertices.Count == 0) return;

        // Update mesh with all accumulated points
        pointCloudMesh.Clear();
        pointCloudMesh.SetVertices(pointCloudVertices);
        pointCloudMesh.SetColors(pointCloudColors);

        // Create indices for point rendering
        int[] indices = new int[pointCloudVertices.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        pointCloudMesh.SetIndices(indices, MeshTopology.Points, 0);
        pointCloudMesh.RecalculateBounds();

        // Update material properties
        UpdatePointCloudMaterial();
    }

    void UpdatePointCloudMaterial()
    {
        if (meshRenderer.material != null)
        {
            meshRenderer.material.SetFloat("_PointSize", pointSize);
            meshRenderer.material.SetColor("_Color", pointColor);
        }
    }

    void HandleInput()
    {
        // Input handling for scanner control
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleScanning();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetScan();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearPointCloud();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ExportPointCloud();
        }
    }

    #region Public Control Methods

    public void StartScanning()
    {
        isScanning = true;
        Debug.Log("3D Scanner started");
    }

    public void StopScanning()
    {
        isScanning = false;
        Debug.Log("3D Scanner stopped");
    }

    public void ToggleScanning()
    {
        isScanning = !isScanning;
        Debug.Log($"3D Scanner {(isScanning ? "started" : "stopped")}");
    }

    public void ResetScan()
    {
        currentScanAngle = 0f;
        ClearPointCloud();
        Debug.Log("Scan reset");
    }

    public void ClearPointCloud()
    {
        pointCloudVertices.Clear();
        pointCloudColors.Clear();
        pointCloudNormals.Clear();
        pointCloudMesh.Clear();
        Debug.Log("Point cloud cleared");
    }

    public void ExportPointCloud()
    {
        // Export to PLY format (simple implementation)
        string plyContent = GeneratePLYFile();
        string filePath = Application.dataPath + $"/PointCloud_{System.DateTime.Now:yyyyMMdd_HHmmss}.ply";

        try
        {
            System.IO.File.WriteAllText(filePath, plyContent);
            Debug.Log($"Point cloud exported to: {filePath}");
            Debug.Log($"Exported {pointCloudVertices.Count} points");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export point cloud: {e.Message}");
        }
    }

    string GeneratePLYFile()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // PLY header
        sb.AppendLine("ply");
        sb.AppendLine("format ascii 1.0");
        sb.AppendLine($"element vertex {pointCloudVertices.Count}");
        sb.AppendLine("property float x");
        sb.AppendLine("property float y");
        sb.AppendLine("property float z");
        sb.AppendLine("property float nx");
        sb.AppendLine("property float ny");
        sb.AppendLine("property float nz");
        sb.AppendLine("property uchar red");
        sb.AppendLine("property uchar green");
        sb.AppendLine("property uchar blue");
        sb.AppendLine("end_header");

        // Vertex data
        for (int i = 0; i < pointCloudVertices.Count; i++)
        {
            Vector3 vertex = pointCloudVertices[i];
            Vector3 normal = pointCloudNormals[i];
            Color color = pointCloudColors[i];

            sb.AppendLine($"{vertex.x} {vertex.y} {vertex.z} {normal.x} {normal.y} {normal.z} " +
                         $"{(int)(color.r * 255)} {(int)(color.g * 255)} {(int)(color.b * 255)}");
        }

        return sb.ToString();
    }

    #endregion

    #region Advanced Features

    // Method to filter point cloud (remove outliers)
    public void FilterPointCloud(float maxNeighborDistance = 0.5f, int minNeighbors = 3)
    {
        List<Vector3> filteredVertices = new List<Vector3>();
        List<Color> filteredColors = new List<Color>();
        List<Vector3> filteredNormals = new List<Vector3>();

        for (int i = 0; i < pointCloudVertices.Count; i++)
        {
            int neighborCount = CountNeighbors(pointCloudVertices[i], maxNeighborDistance);

            if (neighborCount >= minNeighbors)
            {
                filteredVertices.Add(pointCloudVertices[i]);
                filteredColors.Add(pointCloudColors[i]);
                filteredNormals.Add(pointCloudNormals[i]);
            }
        }

        pointCloudVertices = filteredVertices;
        pointCloudColors = filteredColors;
        pointCloudNormals = filteredNormals;

        UpdatePointCloudVisualization();
        Debug.Log($"Filtered point cloud: {filteredVertices.Count} points remaining");
    }

    int CountNeighbors(Vector3 point, float maxDistance)
    {
        int count = 0;
        float sqrMaxDistance = maxDistance * maxDistance;

        foreach (Vector3 otherPoint in pointCloudVertices)
        {
            if ((otherPoint - point).sqrMagnitude <= sqrMaxDistance)
            {
                count++;
            }
        }

        return count;
    }

    // Method to downsample point cloud
    public void DownsamplePointCloud(float voxelSize = 0.1f)
    {
        Dictionary<Vector3Int, List<int>> voxels = new Dictionary<Vector3Int, List<int>>();

        // Group points into voxels
        for (int i = 0; i < pointCloudVertices.Count; i++)
        {
            Vector3 point = pointCloudVertices[i];
            Vector3Int voxelCoord = new Vector3Int(
                Mathf.FloorToInt(point.x / voxelSize),
                Mathf.FloorToInt(point.y / voxelSize),
                Mathf.FloorToInt(point.z / voxelSize)
            );

            if (!voxels.ContainsKey(voxelCoord))
            {
                voxels[voxelCoord] = new List<int>();
            }
            voxels[voxelCoord].Add(i);
        }

        // Create downsampled point cloud (one point per voxel)
        List<Vector3> downsampledVertices = new List<Vector3>();
        List<Color> downsampledColors = new List<Color>();
        List<Vector3> downsampledNormals = new List<Vector3>();

        foreach (var voxel in voxels)
        {
            if (voxel.Value.Count > 0)
            {
                // Use the first point in the voxel
                int index = voxel.Value[0];
                downsampledVertices.Add(pointCloudVertices[index]);
                downsampledColors.Add(pointCloudColors[index]);
                downsampledNormals.Add(pointCloudNormals[index]);
            }
        }

        pointCloudVertices = downsampledVertices;
        pointCloudColors = downsampledColors;
        pointCloudNormals = downsampledNormals;

        UpdatePointCloudVisualization();
        Debug.Log($"Downsampled point cloud: {downsampledVertices.Count} points");
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw scanner field of view
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, fieldOfView, maxDistance, 0.1f, 1f);

        // Draw current scan angle
        if (isScanning)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 dir = Quaternion.Euler(0, currentScanAngle, 0) * Vector3.forward * 2f;
            Gizmos.DrawRay(transform.position, dir);
        }
    }

    void OnGUI()
    {
        // Display scanner status
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"3D Scanner: {(isScanning ? "SCANNING" : "IDLE")}");
        GUILayout.Label($"Points: {PointCount}");
        GUILayout.Label($"Scan Progress: {ScanProgress * 100:F1}%");
        GUILayout.Label("");
        GUILayout.Label("Controls:");
        GUILayout.Label("Space - Toggle scanning");
        GUILayout.Label("R - Reset scan");
        GUILayout.Label("C - Clear point cloud");
        GUILayout.Label("E - Export to PLY");
        GUILayout.EndArea();
    }
}
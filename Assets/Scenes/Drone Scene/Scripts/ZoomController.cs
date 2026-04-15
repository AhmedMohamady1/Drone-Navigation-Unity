using UnityEngine;
using TMPro;

public class ZoomController : MonoBehaviour
{
    [Header("Setup")]
    public Camera droneCamera;

    [Tooltip("Reference to the Input handler.")]
    public GameInput gameInput; 
    
    [Tooltip("Text component to display the current zoom level (e.g., 5x).")]
    public TextMeshProUGUI zoomText;

    [Header("Zoom States")]
    // 1x, 2x, 4x, 8x, 12x, 16x, 20x
    private readonly int[] ZOOM_FACTORS = { 1, 2, 4, 8, 12, 16, 20 };
    
    // Calculated FOV values corresponding to the factors above (60 / factor)
    private float[] FOV_STEPS;
    
    private const float BASE_FOV = 60f; 
    
    // Tracks the current index in the ZOOM_FACTORS array
    private int currentZoomIndex = 0; 

    void Start()
    {
        // Initialization checks...
        if (droneCamera == null)
            droneCamera = GetComponent<Camera>();
        if (gameInput == null)
            Debug.LogError("GameInput reference missing!");

        // 1. Calculate the actual FOV angles for each step
        FOV_STEPS = new float[ZOOM_FACTORS.Length];
        for (int i = 0; i < ZOOM_FACTORS.Length; i++)
        {
            // FOV = Base FOV / Zoom Factor
            FOV_STEPS[i] = BASE_FOV / ZOOM_FACTORS[i];
        }

        // Start at 1x zoom (first element in the array)
        currentZoomIndex = 0; 
        droneCamera.fieldOfView = FOV_STEPS[currentZoomIndex];
        
        // Subscribe to input events
        gameInput.OnZoomInPerformed += GameInput_OnZoomInPerformed;
        gameInput.OnZoomOutPerformed += GameInput_OnZoomOutPerformed;
        
        UpdateZoomDisplay(); 
    }

    void Update()
    {
        // Update the display every frame in case of other camera changes
        UpdateZoomDisplay();
    }

    private void GameInput_OnZoomOutPerformed(object sender, System.EventArgs e)
    {
        // Zoom Out (Decrease Index / Increase FOV)
        
        // Increase the index (move left in the array, toward 1x zoom)
        currentZoomIndex = Mathf.Clamp(currentZoomIndex - 1, 0, FOV_STEPS.Length - 1);
        
        // Apply the new FOV
        droneCamera.fieldOfView = FOV_STEPS[currentZoomIndex];
    }

    private void GameInput_OnZoomInPerformed(object sender, System.EventArgs e)
    {
        // Zoom In (Increase Index / Decrease FOV)

        // Increase the index (move right in the array, toward 20x zoom)
        currentZoomIndex = Mathf.Clamp(currentZoomIndex + 1, 0, FOV_STEPS.Length - 1);
        
        // Apply the new FOV
        droneCamera.fieldOfView = FOV_STEPS[currentZoomIndex];
    }
    
    private void UpdateZoomDisplay()
    {
        if (droneCamera == null || zoomText == null) return;

        // Display the factor directly from the array index
        int displayZoom = ZOOM_FACTORS[currentZoomIndex];
        
        // Update the TextMeshPro component
        zoomText.text = displayZoom.ToString() + "x";
    }

    void OnDestroy()
    {
        if (gameInput != null)
        {
            gameInput.OnZoomInPerformed -= GameInput_OnZoomInPerformed;
            gameInput.OnZoomOutPerformed -= GameInput_OnZoomOutPerformed;
        }
    }
}
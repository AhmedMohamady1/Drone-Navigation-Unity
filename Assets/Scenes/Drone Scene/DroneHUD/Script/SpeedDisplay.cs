using UnityEngine;
using TMPro;

public class SpeedDisplay : MonoBehaviour
{
    [Tooltip("Text component to display the speed.")]
    public TextMeshProUGUI targetText;
    
    // *** NEW REFERENCE: We now require the Rigidbody component ***
    [Tooltip("Drag the drone's GameObject. It MUST have a Rigidbody component attached.")]
    public Rigidbody droneRigidbody; 
    
    // The conversion factor: 1 m/s = 3.6 km/h
    private const float M_S_TO_KMH = 3.6f;

    void Update()
    {
        if (droneRigidbody == null || targetText == null)
        {
            // Ensures safety if references are missing
            return;
        }
        
        // 1. Get Speed directly from the physics engine's calculation (much smoother)
        float speed_ms = droneRigidbody.linearVelocity.magnitude;
        
        // 2. Convert to km/h and round to the nearest whole number
        float speed_kmh = speed_ms * M_S_TO_KMH;

        // 3. Update the text display ("F0" rounds the value)
        targetText.text = speed_kmh.ToString("F0");
    }
}
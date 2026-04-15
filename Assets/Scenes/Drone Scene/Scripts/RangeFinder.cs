using UnityEngine;
using TMPro;

public class RangeFinder : MonoBehaviour
{
    [Tooltip("Text component to display the distance.")]
    public TextMeshProUGUI distanceText;

    [Tooltip("The layer(s) the raycast will check for collision (e.g., Environment, Default).")]
    public LayerMask hitLayers; 

    [Tooltip("Maximum distance to search for objects.")]
    public float maxDistance = 500f; 

    private const string DISTANCE_FORMAT = "F0"; // Format to whole meters (no decimals)

    void Update()
    {
        if (distanceText == null) return;

        // 1. Define the Ray (Fired from the center of the screen)
        // The ray originates from the Camera's position, facing forward.
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // 2. Perform the Raycast
        if (Physics.Raycast(ray, out hit, maxDistance, hitLayers))
        {
            // Ray hit an object!
            
            // Calculate distance (rounded to the nearest whole meter)
            float distance = hit.distance;
            
            // 3. Update Text with Distance
            distanceText.text = distance.ToString(DISTANCE_FORMAT) + "m";
            
            // Optional: Draw the ray in the scene view for debugging 
            Debug.DrawRay(transform.position, transform.forward * hit.distance, Color.red);
        }
        else
        {
            // Ray did NOT hit anything within maxDistance
            distanceText.text = "--";
            
            // Optional: Draw the ray in the scene view to show max range
            Debug.DrawRay(transform.position, transform.forward * maxDistance, Color.blue);
        }
    }
}
using UnityEngine;
using TMPro;

public class PositionLogger : MonoBehaviour
{
    // *** CHANGED from RectTransform to Transform ***
    [Tooltip("Drag the drone's standard Transform component here.")]
    public Transform trackedTransform; 

    [Header("Text Outputs")]
    public TextMeshProUGUI textX;
    public TextMeshProUGUI textY;
    public TextMeshProUGUI textZ;

    void Update()
    {
        // Safety check to ensure the tracked object is set
        if (trackedTransform == null)
        {
            Debug.LogError("PositionLogger: Tracked Transform reference not set! Drag your drone object here.");
            return;
        }

        // Get the current position vector from the 3D world
        Vector3 currentPos = trackedTransform.position;

        // Use a consistent formatting string
        string format = "F0";

        // Update Text X (World Space)
        if (textX != null)
        {
            textX.text = currentPos.x.ToString(format);
        }

        // Update Text Y (World Space)
        if (textY != null)
        {
            textY.text = currentPos.y.ToString(format);
        }

        // Update Text Z (World Space - often height or depth)
        if (textZ != null)
        {
            textZ.text = currentPos.z.ToString(format);
        }
    }
}
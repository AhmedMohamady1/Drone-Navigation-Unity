using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompassController : MonoBehaviour
{
    [Header("References")]
    public RawImage compassRawImage;
    public TextMeshProUGUI headingText;
    public Transform droneTransform;

    [Header("Settings")]
    [Range(0.01f, 1f)]
    public float viewWidth = 0.25f; 
    
    [Header("Calibration")]
    [Range(0f, 1f)]
    public float magneticOffset = 0.631f;

    private Material compassMat;

    void Start()
    {
        if (compassRawImage != null)
            compassMat = compassRawImage.material; 
    }

    void Update()
    {
        if (droneTransform == null) return;

        // 1. Get Heading (0 to 360 inclusive, including floating point values like 359.999)
        float heading = droneTransform.eulerAngles.y;

        // 2. Calculate Display Heading (Guaranteed 0 to 359, skipping 360)
        float displayHeading = heading;
        
        if (displayHeading >= 359.5f)
        {
            displayHeading = 0f;
        }

        // 3. Update Text
        if (headingText != null)
        {
            // Now, displayHeading will be:
            // - If heading is 359.4, displayHeading is 359.4 -> ToString("F0") = "359"
            // - If heading is 359.5, displayHeading is 0.0 -> ToString("F0") = "0"
            // This successfully skips the number 360.
            headingText.text = "<mspace=0.6em>" + displayHeading.ToString("F0") + "°</mspace>"; 
        }

        // 4. Update Compass Strip (Scrolling must use the original heading for continuity)
        if (compassMat != null)
        {
            float scrollValue = heading / 360f;

            // We add the magneticOffset here to shift the texture
            float finalScroll = scrollValue - (viewWidth / 2f) + magneticOffset;
            
            compassMat.SetFloat("_ScrollX", finalScroll);
            compassMat.SetFloat("_ViewWidth", viewWidth);
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; 

public class BatteryDisplay : MonoBehaviour
{
    [Header("Setup")]
    public Image batteryImageComponent; 
    public TextMeshProUGUI percentTextComponent; 
    
    [Tooltip("Drag your 4 sprites here. Order: Empty -> Low -> Med -> Full")]
    public Sprite[] batterySprites; 

    [Header("Debug / Testing")]
    [Range(0, 100)]
    public float currentBatteryPercent = 100f;

    [Header("Flicker Settings")]
    // *** CHANGED: The threshold is now 10% ***
    [Tooltip("The threshold at which the battery starts flickering (e.g., 10).")]
    public float CRITICAL_THRESHOLD = 10f; 
    
    // *** CHANGED: The rate is slower (0.5s instead of 0.2s) ***
    [Tooltip("Time delay between flashes (slower flicker).")]
    public float flickerRate = 0.5f; 

    private Coroutine flickerCoroutine;

    void Update()
    {
        UpdateBatteryIcon();
        UpdateBatteryText(); 
    }

    void UpdateBatteryIcon()
    {
        if (batterySprites.Length == 0 || batteryImageComponent == null) return;

        int index = 0;
        
        // Note: The thresholds for the icon graphic remain at 25%, 50%, 75% for visual scaling,
        // but the flicker threshold is now separate (10%).
        
        if (currentBatteryPercent > 75f)
        {
            index = 3; 
        }
        else if (currentBatteryPercent > 50f)
        {
            index = 2; 
        }
        else if (currentBatteryPercent > 25f)
        {
            index = 1; 
        }
        else 
        {
            index = 0; 
        }

        // *** FLICKER LOGIC: Only start when BELOW or EQUAL to 10% ***
        if (currentBatteryPercent <= CRITICAL_THRESHOLD && flickerCoroutine == null)
        {
            flickerCoroutine = StartCoroutine(FlickerIcon());
        }

        // Stop the flicker if the battery recharges above 10%
        if (currentBatteryPercent > CRITICAL_THRESHOLD && flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
            // Ensure the icon is fully visible when stopped
            batteryImageComponent.color = Color.white; 
        }

        // Only update the sprite if it has changed
        if (batteryImageComponent.sprite != batterySprites[index])
        {
            batteryImageComponent.sprite = batterySprites[index];
        }
    }
    
    IEnumerator FlickerIcon()
    {
        while (true)
        {
            Color currentColor = batteryImageComponent.color;
            
            // Toggle visibility by changing the color's alpha
            if (currentColor.a > 0.5f)
            {
                // Dim/Flicker Off (set alpha to 0.3f)
                batteryImageComponent.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.3f);
            }
            else
            {
                // Flicker On (set to full visibility)
                batteryImageComponent.color = Color.white;
            }
            
            // Wait for the slower, more noticeable rate
            yield return new WaitForSeconds(flickerRate);
        }
    }
    
    void UpdateBatteryText()
    {
        if (percentTextComponent != null)
        {
            percentTextComponent.text = Mathf.RoundToInt(currentBatteryPercent).ToString() + "%";
        }
    }

    public void SetBatteryLevel(float percent)
    {
        currentBatteryPercent = percent;
    }
    
    private void OnDisable()
    {
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
        }
        if (batteryImageComponent != null)
        {
            batteryImageComponent.color = Color.white;
        }
    }
}
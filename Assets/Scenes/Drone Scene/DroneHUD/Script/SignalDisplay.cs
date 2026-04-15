using UnityEngine;
using UnityEngine.UI;
using System.Collections; // Required for Coroutines

public class SignalDisplay : MonoBehaviour
{
    [Header("Setup")]
    public Image signalImageComponent;
    
    [Tooltip("Drag your 5 sprites here. Order: 0 Bars -> 1 Bar -> 2 Bars -> 3 Bars -> 4 Bars")]
    public Sprite[] signalSprites; 

    [Header("Debug / Testing")]
    [Range(0, 4)] // Signal strength is typically 0 to 4
    public int currentSignalBars = 4;

    [Header("Flicker Settings")]
    [Tooltip("The lowest bar count (usually 0) to start flickering.")]
    public int CRITICAL_SIGNAL_LEVEL = 0; 
    [Tooltip("Time delay between flashes.")]
    public float flickerRate = 0.5f; // A medium rate for a warning

    private Coroutine flickerCoroutine; // Holds the reference to the running coroutine

    void Update()
    {
        UpdateSignalIcon();
    }

    void UpdateSignalIcon()
    {
        if (signalSprites.Length == 0 || signalImageComponent == null) return;

        // Ensure the input value is within the bounds of our sprite array
        int index = Mathf.Clamp(currentSignalBars, 0, signalSprites.Length - 1);
        
        // --- Flicker Logic ---
        if (currentSignalBars <= CRITICAL_SIGNAL_LEVEL && flickerCoroutine == null)
        {
            // Start flickering when signal is at 0 bars
            flickerCoroutine = StartCoroutine(FlickerIcon());
        }
        else if (currentSignalBars > CRITICAL_SIGNAL_LEVEL && flickerCoroutine != null)
        {
            // Stop flickering when signal is restored (1 bar or higher)
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
            // Ensure the icon is fully visible when the warning stops
            signalImageComponent.color = Color.white; 
        }
        // ---------------------

        // Update the sprite
        if (signalImageComponent.sprite != signalSprites[index])
        {
            signalImageComponent.sprite = signalSprites[index];
        }
    }
    
    // --- NEW COROUTINE FOR FLICKERING ---
    IEnumerator FlickerIcon()
    {
        while (true) // Loop indefinitely while the signal is low
        {
            // Toggle visibility by changing the color's alpha
            Color currentColor = signalImageComponent.color;
            
            // Toggle between fully visible (white) and dimmed (low alpha)
            if (currentColor.a > 0.5f)
            {
                // Dim/Flicker Off (low alpha for quick blink)
                signalImageComponent.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.2f);
            }
            else
            {
                // Flicker On (full visibility)
                signalImageComponent.color = Color.white;
            }
            
            // Wait for the specified rate
            yield return new WaitForSeconds(flickerRate);
        }
    }
    
    // Call this function from your actual Drone Controller later
    public void SetSignalBars(int bars)
    {
        currentSignalBars = bars;
    }

    private void OnDisable()
    {
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
        }
        if (signalImageComponent != null)
        {
            signalImageComponent.color = Color.white;
        }
    }
}
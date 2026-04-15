using UnityEngine;
using UnityEngine.UI;

public class DroneHUD_Toggle : MonoBehaviour
{
    [Header("Drone References")]
    public Rigidbody droneRigidbody;

    [Header("THE SCENE OBJECTS")]
    // Drag the 4 objects from your HIERARCHY (Left panel) into these slots.
    // Do NOT drag the PNG files from the Project panel!
    public GameObject batFullObj;
    public GameObject batHighObj;
    public GameObject batMedObj;
    public GameObject batLowObj;

    [Header("Hard Coded Position")]
    // X = 800 (Right), Y = 400 (Top)
    private Vector2 fixedPosition = new Vector2(800f, 400f);

    [Header("Simulated Data")]
    [Range(0, 100)] public float batteryPercent = 89f;

    void Start()
    {
        // --- HARD CODE POSITIONS ---
        // We use GetComponent to find the position controller (RectTransform) automatically
        SetPos(batFullObj);
        SetPos(batHighObj);
        SetPos(batMedObj);
        SetPos(batLowObj);
    }

    void SetPos(GameObject obj)
    {
        if (obj != null)
        {
            // This grabs the RectTransform so we can move it
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = fixedPosition;
        }
    }

    void Update()
    {
        // 1. Turn EVERYTHING off
        if(batFullObj) batFullObj.SetActive(false);
        if(batHighObj) batHighObj.SetActive(false);
        if(batMedObj) batMedObj.SetActive(false);
        if(batLowObj) batLowObj.SetActive(false);

        // 2. Turn ON the right one
        if (batteryPercent > 75f)
        {
            if(batFullObj) batFullObj.SetActive(true);
        }
        else if (batteryPercent > 50f)
        {
            if(batHighObj) batHighObj.SetActive(true);
        }
        else if (batteryPercent > 25f)
        {
            if(batMedObj) batMedObj.SetActive(true);
        }
        else
        {
            if(batLowObj) batLowObj.SetActive(true);
        }
    }
}
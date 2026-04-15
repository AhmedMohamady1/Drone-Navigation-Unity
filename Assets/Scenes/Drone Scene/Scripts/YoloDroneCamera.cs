using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine; 

public class YoloDroneCamera : MonoBehaviour
{
    [Header("Inference Engine Settings")]
    public ModelAsset yoloModelAsset;
    public Camera droneCamera;

    [Header("YOLO Settings")]
    public float confidenceThreshold = 0.5f;
    private const int ImageSize = 640;

    [Header("Performance & UI")]
    [Range(1, 60)] public int inferenceFPS = 15;
    private float timer = 0f;

    private Worker worker;
    private RenderTexture targetRT;
    private List<BoundingBox> currentBoxes = new List<BoundingBox>();

    // Custom Dataset Labels (72 classes)
    private readonly string[] labels = {
        "Beaver", "BrichTree", "Lynx", "Marten", "Squirrel", "Warbler", "Woodpecker",
        "AbandonedCar", "Bicycle", "CalicoCat", "Coyote", "Crow", "FeralDog", "Pigeon",
        "Rat", "Skeleton", "Agave", "DesertBighornSheep", "Tortoise", "DesertWillow",
        "DorcasGazelle", "Pelican", "RattleSnake", "SeaBird", "AlpineMarmot", "Elk",
        "GoldenEagle", "GrizzlyBear", "Heather", "MountainLion", "Bison",
        "Black-footedFerret", "Hyena", "Lion", "TurtleOrnateBox", "Pipit", "Elephant",
        "Quail", "Zebra", "BigSnake", "CacaoTree", "Capybara", "Gorilla",
        "GreenAnaconda", "GreenIguana", "Leopard", "Okapi", "Pangolin",
        "PygmyChimpanzee", "Sloth", "AloeVeraPlant", "DesertScorpion",
        "DromedaryCamel", "FennecFox", "Gecko", "HornedLizard", "Jerboa",
        "SalviaPlant", "AmericanBlackBear", "Hickory", "Maple", "Raccoon", "RedFox",
        "White-tailedDeer", "WoodFrog", "Cypress", "Cactus", "PricklyPearCactus",
        "FlowerGazania", "FlowerEmpodium", "Conifer", "PineTree"
    };

    public struct BoundingBox
    {
        public float xMin, yMin, xMax, yMax;
        public float confidence;
        public int classId;
    }

    void Start()
    {
        Model model = ModelLoader.Load(yoloModelAsset);
        worker = new Worker(model, BackendType.GPUCompute); 
        targetRT = new RenderTexture(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32);
    }

    void Update()
    {
        if (droneCamera == null || worker == null) return;

        timer += Time.deltaTime;
        if (timer >= 1f / inferenceFPS)
        {
            timer = 0f;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture prevCameraTarget = droneCamera.targetTexture;
            
            // 1. Cache the exact widescreen aspect ratio your screen is currently using
            float originalAspect = droneCamera.aspect;
            
            droneCamera.targetTexture = targetRT;
            
            // 2. Force the camera to stay widescreen even though it's rendering into a square texture
            droneCamera.aspect = originalAspect; 
            
            droneCamera.Render();
            
            // 3. Reset the camera back to normal so your main game view doesn't break
            droneCamera.targetTexture = prevCameraTarget;
            droneCamera.ResetAspect();
            RenderTexture.active = prevActive;

            using Tensor<float> yoloInputTensor = new Tensor<float>(new TensorShape(1, 3, ImageSize, ImageSize));
            TextureConverter.ToTensor(targetRT, yoloInputTensor);
            
            worker.Schedule(yoloInputTensor);
            
            Tensor<float> yoloOutputTensor = worker.PeekOutput() as Tensor<float>;
            currentBoxes = ParseYoloOutput(yoloOutputTensor);
        }
    }

    List<BoundingBox> ParseYoloOutput(Tensor<float> tensor)
    {
        // For a [1, 300, 6] tensor, dim1 is 300 (boxes)
        int numBoxes = tensor.shape[1]; 
        
        float[] data = tensor.DownloadToArray();
        List<BoundingBox> candidates = new List<BoundingBox>();

        for (int i = 0; i < numBoxes; i++)
        {
            // The 6 values are natively exported as: [xMin, yMin, xMax, yMax, Confidence, ClassId]
            int boxOffset = i * 6;
            float conf = data[boxOffset + 4];

            if (conf >= confidenceThreshold)
            {
                candidates.Add(new BoundingBox
                {
                    // Normalize the coordinates from the 640px space back to 0.0-1.0 scale
                    xMin = data[boxOffset + 0] / ImageSize,
                    yMin = data[boxOffset + 1] / ImageSize,
                    xMax = data[boxOffset + 2] / ImageSize,
                    yMax = data[boxOffset + 3] / ImageSize,
                    confidence = conf,
                    classId = (int)data[boxOffset + 5] 
                });
            }
        }

        return candidates; 
    }

    void OnGUI()
    {
        if (currentBoxes == null || currentBoxes.Count == 0) return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        int lineThickness = 2; 

        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 16;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.white;

        foreach (var box in currentBoxes)
        {
            float x = box.xMin * screenW;
            float y = box.yMin * screenH;
            float w = (box.xMax - box.xMin) * screenW;
            float h = (box.yMax - box.yMin) * screenH;

            GUI.color = Color.green;

            GUI.DrawTexture(new Rect(x, y, w, lineThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - lineThickness, w, lineThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, lineThickness, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w - lineThickness, y, lineThickness, h), Texture2D.whiteTexture);
            
            string labelName = box.classId < labels.Length ? labels[box.classId] : $"Class {box.classId}";
            string displayText = $"{labelName}: {(box.confidence * 100):0}%";
            
            Vector2 textSize = textStyle.CalcSize(new GUIContent(displayText));
            Rect bgRect = new Rect(x, y - textSize.y - 4, textSize.x + 8, textSize.y + 4);
            
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 4, y - textSize.y - 2, textSize.x, textSize.y), displayText, textStyle);
        }
    }

    void OnDisable()
    {
        worker?.Dispose();
        if (targetRT != null) targetRT.Release();
    }
}
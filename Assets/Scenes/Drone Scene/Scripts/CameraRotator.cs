using UnityEngine;


public class CameraRotator : MonoBehaviour
{
    [SerializeField]
    private GameInput gameInput;

    [SerializeField]
    private Transform cameraTransformUPAndDown; // Handles PITCH (up/down)

    [SerializeField]
    private Transform hingTransformRightAndLeft; // Handles YAW (left/right)

    [Header("Rotation Settings")]
    public float rotationSpeed = 100f;

    [Header("Rotation Limits (Degrees)")]
    public float minPitch = -45f;
    public float maxPitch = 45f;
    public float minYaw = -45f;
    public float maxYaw = 45f;

    private float initialHingYaw;
    private float initialHingPitch;
    private float initialHingRoll;

    private float currentPitch = 0f;
    private float currentYaw = 0f;

    void Start()
    {
        // Store the initial hinge rotation offsets
        if (hingTransformRightAndLeft != null)
        {
            Vector3 initialHingRotation = hingTransformRightAndLeft.localEulerAngles;
            initialHingYaw = NormalizeAngle(initialHingRotation.y);
            initialHingPitch = NormalizeAngle(initialHingRotation.x);
            initialHingRoll = NormalizeAngle(initialHingRotation.z);
        }

        // Store initial camera pitch
        if (cameraTransformUPAndDown != null)
        {
            currentPitch = NormalizeAngle(cameraTransformUPAndDown.localEulerAngles.x);
        }
    }

    void Update()
    {
        if (cameraTransformUPAndDown == null || hingTransformRightAndLeft == null)
            return;

        // Get input
        Vector2 input = gameInput.DroneCamMovmentNormalized();
        float horizontal = input.x; // Left/Right for YAW
        float vertical = input.y;   // Up/Down for PITCH

        // Calculate rotation deltas
        float yawDelta = horizontal * rotationSpeed * Time.deltaTime;
        float pitchDelta = vertical * rotationSpeed * Time.deltaTime;

        // Apply rotation with limits
        ApplyRotationWithLimits(pitchDelta, yawDelta);
    }

    void ApplyRotationWithLimits(float pitchDelta, float yawDelta)
    {
        // Update current rotations (these are offsets from initial)
        currentYaw += yawDelta;
        currentPitch += pitchDelta;

        // Apply limits to the offsets
        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Apply rotations to transforms while preserving initial rotations
        // Hing: YAW rotation around Y axis + initial rotation
        hingTransformRightAndLeft.localEulerAngles = new Vector3(
            initialHingPitch,
            initialHingYaw + currentYaw,
            initialHingRoll
        );

        // Camera: PITCH rotation around X axis
        cameraTransformUPAndDown.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
    }

    // Normalizes angle to -180 to 180 range for easier clamping
    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
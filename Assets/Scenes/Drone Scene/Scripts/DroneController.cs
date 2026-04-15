using System;
using UnityEngine;

public class DroneController : MonoBehaviour
{
    [SerializeField]
    private GameInput gameInput;

    [Header("Movement Physics")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float horizontalDamping = 0.5f; // Quick stopping
    [SerializeField] private float horizontalDrag = 1f; // Continuous air resistance
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Collision Stability")]
    [SerializeField] private CollisionDetectionMode collisionMode = CollisionDetectionMode.ContinuousSpeculative;
    [SerializeField] private RigidbodyInterpolation interpolationMode = RigidbodyInterpolation.Interpolate;
    [SerializeField] private float maxTotalSpeed = 20f;
    [SerializeField] private float maxDepenetrationVelocity = 6f;



    [Header("Stabilization")]
    public float hoverThrust = 9.81f;
    public float thrustPower = 20f;
    public float damping = 2f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.3f;

    [Header("Propellers")]
    [Range(0, 1), SerializeField]
    public float propellerSpeed = 0f;

    private Rigidbody rb;
    private float yawRotation = 0f;
    private float defaultYawRotation = 0f;
    public bool isGrounded = false;

    private void OnEnable()
    {
        if (gameInput != null)
        {
            gameInput.OnRecenterPerformed += GameInput_OnRecenterPerformed;
        }
    }

    private void OnDisable()
    {
        if (gameInput != null)
        {
            gameInput.OnRecenterPerformed -= GameInput_OnRecenterPerformed;
        }
    }


    void Start()
    {
        rb = this.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("DroneController requires a Rigidbody component.");
            enabled = false;
            return;
        }

        rb.collisionDetectionMode = collisionMode;
        rb.interpolation = interpolationMode;

        // Keep this moderate; too large can cause visible jitter when pressed against walls.
        rb.maxDepenetrationVelocity = Mathf.Max(1f, maxDepenetrationVelocity);

        // Freeze ALL rotations to prevent physics solver from fighting our MoveRotation logic on wall impacts
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Give the drone 0 friction so it smoothly slides across walls without stick-slip jittering
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicsMaterial smoothMat = new PhysicsMaterial("SmoothDrone");
            smoothMat.dynamicFriction = 0f;
            smoothMat.staticFriction = 0f;
            smoothMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            smoothMat.bounciness = 0f;
            smoothMat.bounceCombine = PhysicsMaterialCombine.Minimum;
            col.material = smoothMat;
        }

        yawRotation = transform.eulerAngles.y;
        defaultYawRotation = yawRotation;


    }

    private void GameInput_OnRecenterPerformed(object sender, EventArgs e)
    {
        RecenterToDefaultAngle();
    }

    private void RecenterToDefaultAngle()
    {
        yawRotation = defaultYawRotation;
        rb.MoveRotation(Quaternion.Euler(0f, yawRotation, 0f));

        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }


    }


    void FixedUpdate()
    {
        CheckGround();
        NewHandleMovement();
        UpdatePropellerSpeed();

    }

    void CheckGround()
    {
        // Ignore steep surfaces so side-wall contacts do not count as grounded.
        if (Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            isGrounded = Vector3.Dot(hit.normal, Vector3.up) > 0.6f;
        }
        else
        {
            isGrounded = false;
        }
    }


    private void NewHandleMovement()
    {
        Vector2 leftStick = gameInput.DroneLeftJoyStickMovmentNormalized();
        Vector2 rightStick = gameInput.DroneRightJoyStickMovmentNormalized();

        if (!isGrounded)
        {
 // Yaw rotation
        yawRotation += leftStick.x * rotateSpeed * Time.fixedDeltaTime;
         rb.MoveRotation(Quaternion.Euler(0f, yawRotation, 0f));
        }
       

        // Calculate desired movement direction in world space
        Vector3 desiredLocalMovement = new Vector3(
            rightStick.x,
            0,
            rightStick.y
        );
        Vector3 desiredWorldDirection = transform.TransformDirection(desiredLocalMovement);

        // Get current horizontal velocity
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        // Apply continuous air resistance/drag
        if (horizontalVelocity.magnitude > 0.1f)
        {
            Vector3 dragForce = -horizontalVelocity.normalized * (horizontalDrag * horizontalVelocity.magnitude);
            rb.AddForce(dragForce, ForceMode.Acceleration);
        }
        if (!isGrounded)
        {
            // Apply additional damping when no input is detected
            if (desiredWorldDirection.magnitude < 0.1f && horizontalVelocity.magnitude > 0.1f)
            {
                rb.AddForce(-horizontalVelocity * horizontalDamping, ForceMode.Acceleration);
            }

            // Calculate acceleration force
            if (desiredWorldDirection.magnitude > 0.1f)
            {
                // Apply acceleration in desired direction
                Vector3 accelerationForce = desiredWorldDirection.normalized * acceleration;
                rb.AddForce(accelerationForce, ForceMode.Acceleration);

                // Clamp horizontal velocity
                Vector3 newHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (newHorizontalVelocity.magnitude > maxSpeed)
                {
                    Vector3 excess = newHorizontalVelocity - newHorizontalVelocity.normalized * maxSpeed;
                    rb.AddForce(-excess / Mathf.Max(Time.fixedDeltaTime, 0.0001f), ForceMode.Acceleration);
                }
            }


        }




        // --- VERTICAL THRUST (hover + ascend/descend) ---
        float verticalInput = leftStick.y; // -1 to 1

        // Base hover thrust to counteract gravity
        float thrust = hoverThrust;

        // Add extra thrust for ascending/descending

        thrust += verticalInput * thrustPower;


        rb.AddForce(Vector3.up * thrust, ForceMode.Acceleration);

        // --- DAMPING (simulate air resistance) ---
        // Reduce velocity over time when no input
        Vector3 currentVelocity = rb.linearVelocity;

        // Damp horizontal movement
        Vector3 horizontalVel = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        rb.AddForce(-horizontalVel * damping, ForceMode.Acceleration);

        // Optional: Damp vertical drift slightly (but keep hover stable)
        if (Mathf.Abs(verticalInput) < 0.1f && !isGrounded)
        {
            rb.AddForce(-Vector3.up * currentVelocity.y * damping * 0.5f, ForceMode.Acceleration);
        }

        // --- GROUND HANDLING ---
        if (isGrounded && rb.linearVelocity.y < 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }

        // Final safety clamp to avoid occasional velocity spikes that can tunnel through thin geometry.
        float speedSq = rb.linearVelocity.sqrMagnitude;
        float maxSpeedSq = maxTotalSpeed * maxTotalSpeed;
        if (speedSq > maxSpeedSq)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxTotalSpeed;
        }
    }


    public void UpdatePropellerSpeed()
    {
        if (isGrounded)
        {
            // Fully stopped when landed
            propellerSpeed = Mathf.MoveTowards(propellerSpeed, 0f, 1000);
        }
        else
        {
            //// Calculate how much thrust we're using
            //float verticalInput = gameInput.DroneLeftJoyStickMovmentNormalized().y;
            //float thrustLevel = Mathf.Abs(verticalInput); // 0 = hover, 1 = full ascend/descend

            //// Base hover requires some spin, so minimum is ~0.3–0.5
            //float targetSpeed = Mathf.Lerp(0.4f, 1f, thrustLevel); // Hover = 0.4, Full thrust = 1

            //propellerSpeed = Mathf.Lerp(propellerSpeed, targetSpeed, Time.fixedDeltaTime * 8f);



            // Get current input
            Vector2 leftStick = gameInput.DroneLeftJoyStickMovmentNormalized();   // Y = vertical, X = yaw
            Vector2 rightStick = gameInput.DroneRightJoyStickMovmentNormalized(); // X = strafe, Y = forward

            // 1. Vertical thrust level (from -1 to +1, but we care about magnitude for effort)
            float verticalInput = leftStick.y; // +1 = up, -1 = down
            float verticalEffort = Mathf.Abs(verticalInput); // How hard we're pushing up/down

            // 2. Horizontal movement effort (how fast we're trying to move sideways/forward)
            float horizontalInputMagnitude = rightStick.magnitude; // 0 to 1
            horizontalInputMagnitude = Mathf.Clamp01(horizontalInputMagnitude);

            // 3. Combine efforts: moving fast horizontally requires more thrust (due to tilt & drag)
            // Even at hover (no input), we need base spin
            float totalEffort = Mathf.Max(verticalEffort, horizontalInputMagnitude);

            // Base spin when idle but airborne (e.g., 0.3–0.4)
            float baseSpin = isGrounded ? 0f : 0.35f;

            // Target speed: base + extra based on total effort
            float targetSpeed = baseSpin + (1f - baseSpin) * totalEffort;
            targetSpeed = Mathf.Clamp01(targetSpeed);

            // Smooth interpolation
            float lerpSpeed = isGrounded ? 10f : 8f;
            propellerSpeed = Mathf.Lerp(propellerSpeed, targetSpeed, Time.deltaTime * lerpSpeed);
        }
    }

    // Optional: Visualize ground check in editor
    void OnDrawGizmos()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
    //void HandleMovement()
    //{
    //    Vector2 leftInput = gameInput.DroneLeftJoyStickMovmentNormalized();
    //    Debug.Log(leftInput);
    //    xRotation += leftInput.x;
    //    yLaviation += leftInput.y;
    //    Vector3 direction = new Vector3(0, yLaviation, 0).normalized;
    //    rb.linearVelocity = direction * moveSpeed * Time.deltaTime;
    //    rb.
    //    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, xRotation * rotateSpeed, 0), Time.deltaTime * rotateSpeed);


    //}


}

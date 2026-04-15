using UnityEngine;

/// <summary>
/// Shared Rigidbody drone physics used by both manual and ML control paths.
/// </summary>
[DisallowMultipleComponent]
public class SharedDronePhysics : MonoBehaviour
{
    [Header("Movement Physics")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float horizontalDamping = 0.5f;
    [SerializeField] private float horizontalDrag = 1f;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Stabilization")]
    [SerializeField] private float hoverThrust = 9.81f;
    [SerializeField] private float thrustPower = 20f;
    [SerializeField] private float damping = 2f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.3f;

    [Header("Behavior")]
    [SerializeField] private bool allowYawWhenGrounded = false;

    private Rigidbody _rb;
    private float _yawRotation;
    private Vector2 _horizontalInput;
    private float _verticalInput;
    private float _yawInput;

    public bool IsGrounded { get; private set; }

    [Range(0f, 1f)]
    public float PropellerSpeed { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _yawRotation = transform.eulerAngles.y;

        if (_rb == null)
            Debug.LogError("[SharedDronePhysics] Rigidbody not found on drone GameObject.");
    }

    public void SetControlInput(Vector2 horizontalInput, float verticalInput, float yawInput)
    {
        _horizontalInput = Vector2.ClampMagnitude(horizontalInput, 1f);
        _verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        _yawInput = Mathf.Clamp(yawInput, -1f, 1f);
    }

    public void SimulateStep()
    {
        if (_rb == null)
            return;

        CheckGround();
        HandleMovement();
        UpdatePropellerSpeed();
    }

    public float GroundCheckDistance => groundCheckDistance;

    public void Configure(
        float acceleration,
        float maxSpeed,
        float horizontalDamping,
        float horizontalDrag,
        float rotateSpeed,
        float hoverThrust,
        float thrustPower,
        float damping,
        LayerMask groundLayer,
        float groundCheckDistance,
        bool allowYawWhenGrounded)
    {
        this.acceleration = Mathf.Max(0f, acceleration);
        this.maxSpeed = Mathf.Max(0f, maxSpeed);
        this.horizontalDamping = Mathf.Max(0f, horizontalDamping);
        this.horizontalDrag = Mathf.Max(0f, horizontalDrag);
        this.rotateSpeed = Mathf.Max(0f, rotateSpeed);

        this.hoverThrust = hoverThrust;
        this.thrustPower = thrustPower;
        this.damping = Mathf.Max(0f, damping);

        this.groundLayer = groundLayer;
        this.groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        this.allowYawWhenGrounded = allowYawWhenGrounded;
    }

    public void ResetRuntimeState()
    {
        _horizontalInput = Vector2.zero;
        _verticalInput = 0f;
        _yawInput = 0f;
        _yawRotation = transform.eulerAngles.y;
        PropellerSpeed = 0f;
    }

    private void CheckGround()
    {
        int mask = groundLayer.value == 0 ? Physics.DefaultRaycastLayers : groundLayer.value;
        IsGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, mask);
    }

    private void HandleMovement()
    {
        if (!IsGrounded || allowYawWhenGrounded)
        {
            _yawRotation += _yawInput * rotateSpeed * Time.fixedDeltaTime;
            transform.rotation = Quaternion.Euler(0f, _yawRotation, 0f);
        }

        Vector3 desiredLocalMovement = new Vector3(_horizontalInput.x, 0f, _horizontalInput.y);
        Vector3 desiredWorldDirection = transform.TransformDirection(desiredLocalMovement);

        Vector3 horizontalVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        if (horizontalVelocity.magnitude > 0.1f)
        {
            Vector3 dragForce = -horizontalVelocity.normalized * (horizontalDrag * horizontalVelocity.magnitude);
            _rb.AddForce(dragForce, ForceMode.Acceleration);
        }

        if (!IsGrounded)
        {
            if (desiredWorldDirection.magnitude < 0.1f && horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 dampedVelocity = horizontalVelocity * (1f - horizontalDamping * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector3(dampedVelocity.x, _rb.linearVelocity.y, dampedVelocity.z);
            }

            if (desiredWorldDirection.magnitude > 0.1f)
            {
                Vector3 accelerationForce = desiredWorldDirection.normalized * acceleration;
                _rb.AddForce(accelerationForce, ForceMode.Acceleration);

                Vector3 newHorizontalVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                if (newHorizontalVelocity.magnitude > maxSpeed)
                {
                    Vector3 clampedVelocity = newHorizontalVelocity.normalized * maxSpeed;
                    _rb.linearVelocity = new Vector3(clampedVelocity.x, _rb.linearVelocity.y, clampedVelocity.z);
                }
            }
        }

        float thrust = hoverThrust + (_verticalInput * thrustPower);
        _rb.AddForce(Vector3.up * thrust, ForceMode.Acceleration);

        Vector3 currentVelocity = _rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        _rb.linearVelocity -= horizontalVel * damping * Time.fixedDeltaTime;

        if (Mathf.Abs(_verticalInput) < 0.1f && !IsGrounded)
        {
            _rb.linearVelocity -= Vector3.up * currentVelocity.y * damping * 0.5f * Time.fixedDeltaTime;
        }

        if (IsGrounded && _rb.linearVelocity.y < 0f)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        }
    }

    private void UpdatePropellerSpeed()
    {
        if (IsGrounded)
        {
            PropellerSpeed = Mathf.MoveTowards(PropellerSpeed, 0f, 1000f);
            return;
        }

        float verticalEffort = Mathf.Abs(_verticalInput);
        float horizontalInputMagnitude = Mathf.Clamp01(_horizontalInput.magnitude);
        float totalEffort = Mathf.Max(verticalEffort, horizontalInputMagnitude);

        float baseSpin = 0.35f;
        float targetSpeed = baseSpin + (1f - baseSpin) * totalEffort;
        targetSpeed = Mathf.Clamp01(targetSpeed);

        float lerpSpeed = 8f;
        PropellerSpeed = Mathf.Lerp(PropellerSpeed, targetSpeed, Time.deltaTime * lerpSpeed);
    }
}

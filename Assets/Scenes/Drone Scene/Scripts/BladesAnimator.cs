using UnityEngine;

public class BladesAnimator : MonoBehaviour
{
    [SerializeField] private DroneController droneController;

    [SerializeField] private Transform blade1Transform;
    [SerializeField] private Transform blade2Transform;
    [SerializeField] private Transform blade3Transform;
    [SerializeField] private Transform blade4Transform;

    [SerializeField] private float maxPropellerSpeed = 2000f; // degrees per second (adjust as needed)
    [SerializeField] private float spinUpTime = 1f;          // time to reach full speed
    [SerializeField] private float spinDownTime = 0.8f;      // time to stop when grounded

    private float currentSpeed = 0f;

    void FixedUpdate()
    {
        // Determine target speed based on drone state
        float targetSpeed = droneController.isGrounded ? 0f : maxPropellerSpeed * droneController.propellerSpeed;

        // Smoothly interpolate current speed toward target
        float smoothTime = targetSpeed > currentSpeed ? spinUpTime : spinDownTime;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref currentSpeed, smoothTime * Time.fixedDeltaTime);

        // Alternatively, simpler (but less physically accurate):
        // float acceleration = droneController.isGrounded ? 1f / spinDownTime : 1f / spinUpTime;
        // currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        // Apply rotation to all blades
        float rotationAmount = currentSpeed * Time.fixedDeltaTime;
        blade1Transform.Rotate(Vector3.forward, rotationAmount, Space.Self);
        blade2Transform.Rotate(Vector3.forward, rotationAmount, Space.Self);
        blade3Transform.Rotate(Vector3.forward, rotationAmount, Space.Self);
        blade4Transform.Rotate(Vector3.forward, rotationAmount, Space.Self);
    }
}

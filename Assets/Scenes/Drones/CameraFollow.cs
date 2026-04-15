using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // Aircraft
    public Vector3 offset = new Vector3(0, 3, -8);
    public float smoothSpeed = 5f;
    public float lookSpeed = 10f;

    void LateUpdate()
    {
        if (!target) return;

        // Desired position
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);

        // Smooth movement
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        // Smooth rotation
        Quaternion desiredRotation = Quaternion.LookRotation(
            target.position - transform.position
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            lookSpeed * Time.deltaTime
        );
    }
}
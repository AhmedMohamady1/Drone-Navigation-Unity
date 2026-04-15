using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    // Existing Events
    public event EventHandler OnInteractAction;
    public event EventHandler OnInteractAlternateAction;
    
    // *** NEW ZOOM STEP EVENTS ***
    public event EventHandler OnZoomInPerformed;
    public event EventHandler OnZoomOutPerformed;
    public event EventHandler OnRecenterPerformed;

    PlayerInputAcions playerInputActions; 
    InputAction recenterAction;

    void Awake()
    {
        playerInputActions = new PlayerInputAcions();

        // --- Existing Subscriptions ---
        playerInputActions.Player.interact.performed += Interact_performed;
        playerInputActions.Player.InteractAlternate.performed += InteractAlternate_performed;
        
        // *** NEW: Subscribe to Zoom Actions ***
        // These events are triggered when the R2/L2 buttons are pressed
        playerInputActions.Drone.ZoomIn.performed += ZoomIn_performed;
        playerInputActions.Drone.ZoomOut.performed += ZoomOut_performed;

        recenterAction = playerInputActions.Drone.Get().FindAction("Recenter", false);
        if (recenterAction == null)
        {
            // Do not mutate the generated InputActionAsset at runtime while maps are enabled.
            recenterAction = new InputAction(
                name: "Recenter",
                type: InputActionType.Button,
                binding: "<Keyboard>/t",
                interactions: "Press");
            recenterAction.AddBinding("<Gamepad>/buttonNorth");
        }

        recenterAction.performed += Recenter_performed;

        playerInputActions.Player.Enable();
        playerInputActions.Drone.Enable();

        // Standalone actions must be enabled explicitly.
        if (recenterAction.actionMap == null)
        {
            recenterAction.Enable();
        }
    }

    private void OnDestroy()
    {
        if (playerInputActions == null)
        {
            return;
        }

        playerInputActions.Player.interact.performed -= Interact_performed;
        playerInputActions.Player.InteractAlternate.performed -= InteractAlternate_performed;
        playerInputActions.Drone.ZoomIn.performed -= ZoomIn_performed;
        playerInputActions.Drone.ZoomOut.performed -= ZoomOut_performed;

        if (recenterAction != null)
        {
            recenterAction.performed -= Recenter_performed;
            if (recenterAction.actionMap == null)
            {
                recenterAction.Disable();
                recenterAction.Dispose();
            }
        }

        playerInputActions.Player.Disable();
        playerInputActions.Drone.Disable();

        playerInputActions.Dispose();
    }

    private void Recenter_performed(InputAction.CallbackContext obj)
    {
        OnRecenterPerformed?.Invoke(this, EventArgs.Empty);
    }

    private void ZoomOut_performed(InputAction.CallbackContext obj)
    {
        OnZoomOutPerformed?.Invoke(this, EventArgs.Empty);
    }

    private void ZoomIn_performed(InputAction.CallbackContext obj)
    {
        OnZoomInPerformed?.Invoke(this, EventArgs.Empty);
    }

    private void InteractAlternate_performed(InputAction.CallbackContext obj)
    {
        OnInteractAlternateAction?.Invoke(this, EventArgs.Empty);
    }

    private void Interact_performed(InputAction.CallbackContext context)
    {
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    // --- Existing ReadValue Methods ---

    public Vector2 GetMovementvectorNormalized()
    {
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();
        inputVector = inputVector.normalized;
        return inputVector;
    }

    public Vector2 DroneLeftJoyStickMovmentNormalized()
    {
        Vector2 inputVector = playerInputActions.Drone.LeftJoy.ReadValue<Vector2>();
        inputVector = inputVector.normalized;
        return inputVector;
    }

    public Vector2 DroneRightJoyStickMovmentNormalized()
    {
        Vector2 inputVector = playerInputActions.Drone.RightJoy.ReadValue<Vector2>();
        inputVector = inputVector.normalized;
        return inputVector;
    }

    public Vector2 DroneCamMovmentNormalized()
    {
        Vector2 inputVector = playerInputActions.Drone.headMovement.ReadValue<Vector2>();
        inputVector = inputVector.normalized;
        return inputVector;
    }
}
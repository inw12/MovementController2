using UnityEngine;
public class Player : MonoBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    
    private PlayerInput _inputActions;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // Player Input Actions
        _inputActions = new PlayerInput();
        _inputActions.Enable();
        
        // Player Components
        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraPosition());
    }

    void OnDisable()
    {
        _inputActions.Dispose();
    }

    void Update()
    {
        var deltaTime = Time.deltaTime;
        var input = _inputActions.Default;
        
        // Camera Input
        playerCamera.UpdateRotation(input.Look.ReadValue<Vector2>());

        // Character Inputs
        var characterInput = new CharacterInput
        {
            Rotation    = playerCamera.transform.rotation,
            Movement    = input.Walk.ReadValue<Vector2>(),
            Jump        = input.Jump.WasPressedThisFrame(),
            JumpHold    = input.Jump.IsPressed(),
            Crouch      = input.CrouchHold.IsPressed()
                            ? CrouchInput.Hold
                            : input.CrouchToggle.WasPressedThisFrame()
                                ? CrouchInput.Toggle
                                : input.CrouchHold.WasReleasedThisFrame()
                                    ? CrouchInput.Release
                                    : CrouchInput.None,
            Sprint      = input.Sprint.WasPressedThisFrame(),
            Interact    = input.Interact.WasPerformedThisFrame(),
        };
        playerCharacter.UpdateInput(characterInput);
    }

    void LateUpdate()
    {
        var deltaTime = Time.deltaTime;
        var cameraPosition = playerCharacter.GetCameraPosition();
        
        // Main Camera
        playerCamera.UpdatePosition(cameraPosition);
    }
}

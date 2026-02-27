using UnityEngine;
using KinematicCharacterController;

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector3 Movement;
    public bool Jump;
    public bool JumpHold;
    public CrouchInput Crouch;
    public bool Sprint;
    public bool Interact;
}

public enum CrouchInput
{
    None, Toggle, Hold, Release
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private Transform cameraPosition;

    private KinematicCharacterMotor _motor;
    private bool _inputEnabled;

    // Requested Inputs
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedJumpHold;
    private bool _requestedCrouch;
    private bool _requestedCrouchInAir;
    private bool _requestedSprint;
    private bool _requestedInteract;

    public void Initialize()
    {
        // Initialize Motor
        _motor = GetComponent<KinematicCharacterMotor>();
        _motor.CharacterController = this;

        // Other Variables
        _inputEnabled = true;
    }

    public void UpdateInput(CharacterInput input)
    {
        if (_inputEnabled)
        {
            // Rotation
            _requestedRotation = input.Rotation;

            // Movement
            _requestedMovement = new Vector3(input.Movement.x, 0f, input.Movement.y).normalized;
            _requestedMovement = input.Rotation * _requestedMovement;

            // Jump
            _requestedJump = input.Jump || _requestedJump;
            _requestedJumpHold = input.JumpHold;

            // Crouch
            var wasRequestingCrouch = _requestedCrouch;
            _requestedCrouch = input.Crouch switch
            {
                CrouchInput.Toggle => !_requestedCrouch,
                CrouchInput.Hold => true,
                CrouchInput.Release => false,
                _ => _requestedCrouch
            };
            //if (_requestedCrouch && !wasRequestingCrouch) {
            //    _requestedCrouchInAir = !_state.Grounded;
            //}
            //else if (!_requestedCrouch && wasRequestingCrouch) {
            //    _requestedCrouchInAir = false;
            //}

            // Sprint
            _requestedSprint = input.Sprint ? !_requestedSprint : _requestedSprint;

            // Interact
            _requestedInteract = input.Interact;
        }
    }

    public void UpdateBody()
    {
        
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var lookDirection = Vector3.ProjectOnPlane
        (
            _requestedRotation * Vector3.forward,
            Vector3.up
        );
        currentRotation = Quaternion.LookRotation(lookDirection);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {}

    public void BeforeCharacterUpdate(float deltaTime) {}

    public void AfterCharacterUpdate(float deltaTime) {}

    public bool IsColliderValidForCollisions(Collider coll) => true;

    public void OnDiscreteCollisionDetected(Collider hitCollider) {}

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}

    public void PostGroundingUpdate(float deltaTime) {}

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) {}   

    public Transform GetCameraPosition() => cameraPosition;
}
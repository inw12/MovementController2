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
public struct CharacterState
{
    public CharacterAction CurrentAction;
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    public bool Grounded;
}
public enum CharacterAction
{
    Idle = 0,
    Move = 1,
    Sprint = 2,
    Jump = 3,
    Crouch = 4,
    CrouchMove = 5,
    Slide = 6,
    Mantle = 7,
    Grapple = 8
}

public enum Stance
{
    Stand = 0,
    Crouch = 1,
    Slide = 2
}
public enum CrouchInput
{
    None, Toggle, Hold, Release
}

[RequireComponent(typeof(KinematicCharacterMotor))]
public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [Header("Unity Components")]
    [SerializeField] private Transform cameraPosition;

    [Header("Grounded Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float moveAcceleration = 20f;
    [Space]
    [SerializeField] private float sprintSpeed = 20f;
    [SerializeField] private float sprintAcceleration = 20f;
    [Space]
    [SerializeField] [Range(0f, 1f)] private float crouchSpeedMultiplier = 0.66f;   
    [SerializeField] private float crouchAcceleration = 15f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1f;
    [Space]
    [SerializeField] private float gravity = 25f;
    [SerializeField] [Range(0f, 1f)] private float jumpHoldGravityMultiplier = 0.5f;
    [Space]
    [SerializeField] private float coyoteTime = 0.1f;  // for "coyote-jumping"

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 25f;
    [SerializeField] private float steerLimit = 1f;

    [Header("Crouch")]
    [SerializeField] private float standingCapsuleHeight = 2f;
    [SerializeField] [Range(0f, 1f)] private float standingCameraHeight = 0.8f;
    [Space]
    [SerializeField] private float crouchingCapsuleHeight = 1f;
    [SerializeField] [Range(0f, 1f)] private float crouchingCameraHeight= 0.5f;
    [Space]
    [SerializeField] [Range(0f, 1f)] private float slidingCameraHeight= 0.33f;
    [Space]
    [SerializeField] private float crouchHeightAcceleration = 20f;

    [Header("Slide")]
    [SerializeField] private float slideMinSpeed = 12f;
    [SerializeField] private float slideStartSpeed = 15f;
    [SerializeField] private float slideEndSpeed = 10f;
    [SerializeField] [Min(0.1f)] private float slideFriction = 0.66f;
    [SerializeField] private float slideAcceleration = 50f;             // rate of change in speed when sliding up/down slopes
    [SerializeField] private float slideSteerAcceleration = 30f;        // steering strength while sliding

    private KinematicCharacterMotor _motor;
    private bool _inputEnabled;
    private Collider[] _overlapCollisions;

    // Requested Inputs
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private Vector3 _requestedMovementRaw;
    private bool _requestedJump;
    private bool _requestedJumpHold;
    private bool _requestedCrouch;
    private bool _requestedCrouchInAir;
    private bool _requestedSprint;
    private bool _requestedInteract;

    // State Machine Control
    private CharacterState _state;
    private CharacterState _tempState;
    private CharacterState _prevState;

    // Coyote Jump Variables
    private float _timeSinceUngrounded;
    private float _timeSinceJumpRequest;
    private bool _ungroundedDueToJump;

    // Air Speed Control
    private float _steerTimer;

    public void Initialize()
    {
        // Initialize Motor
        _motor = GetComponent<KinematicCharacterMotor>();
        _motor.CharacterController = this;

        // Initialize State Machine
        _state.Stance = Stance.Stand;

        // Other Variables
        _inputEnabled = true;
        _overlapCollisions = new Collider[10];
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
            _requestedMovementRaw = input.Movement;

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
            if (_requestedCrouch && !wasRequestingCrouch) {
                _requestedCrouchInAir = !_state.Grounded;
            }
            else if (!_requestedCrouch && wasRequestingCrouch) {
                _requestedCrouchInAir = false;
            }

            // Sprint
            _requestedSprint = input.Sprint ? !_requestedSprint : _requestedSprint;

            // Interact
            _requestedInteract = input.Interact;
        }
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = _motor.Capsule.height;

        // Change camera height on crouch/uncrouch
        var targetCameraHeight = _state.Stance switch
        {
            Stance.Crouch => crouchingCameraHeight,
            Stance.Slide => slidingCameraHeight,
            _ => standingCameraHeight
        } * currentHeight;
        cameraPosition.localPosition = Vector3.Lerp
        (
            cameraPosition.localPosition,
            new Vector3(0f, targetCameraHeight, 0f),
            1f - Mathf.Exp(-crouchHeightAcceleration * deltaTime)
        );
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

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // Regular Movement (with inputs ENABLED)
        if (_inputEnabled)
        {
            _state.Acceleration = Vector3.zero;

            // Direction of Movement
            var movementDirection = _motor.GetDirectionTangentToSurface
            (
                _requestedMovement,
                _motor.GroundingStatus.GroundNormal
            );
            // Effective Gravity
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, Vector3.up);
            if (_requestedJumpHold && verticalSpeed > 0f) {
                effectiveGravity *= jumpHoldGravityMultiplier;
            }


            // If, character is on the ground...
            if (_motor.GroundingStatus.IsStableOnGround)
            {
                _timeSinceUngrounded = 0f;
                _steerTimer = 0f;
                _ungroundedDueToJump = false;

                // Update state machine if we landed on the ground when previously jumping
                if (_prevState.CurrentAction is CharacterAction.Jump)
                    _state.CurrentAction = CharacterAction.Idle;

                // Check if eligible to sprint
                _requestedSprint = CanSprint(currentVelocity);

                // SLIDE Start
                if (CanSlide())
                {
                    _state.CurrentAction = CharacterAction.Slide;
                    _state.Stance = Stance.Slide;

                    // Retain velocity when hitting the ground @ high speeds
                    if (!_prevState.Grounded)
                    {
                        currentVelocity = Vector3.ProjectOnPlane
                        (
                            _prevState.Velocity,
                            _motor.GroundingStatus.GroundNormal
                        );
                    }

                    // Calculations to fix "phantom sliding" on unstable/steep ground
                    var effectiveSlideStartSpeed = slideStartSpeed;
                    if (!_prevState.Grounded && !_requestedCrouchInAir)
                    {
                        effectiveSlideStartSpeed = 0f;
                        _requestedCrouchInAir = false;
                    }

                    // Perform slide @ MAX value between current speed and 'slideStartSpeed'
                    var targetSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = _motor.GetDirectionTangentToSurface
                    (
                        currentVelocity,
                        _motor.GroundingStatus.GroundNormal
                    ) * targetSpeed;
                }

                // Continue sliding if stance is "slide"
                if (_state.Stance is Stance.Slide)
                {
                    // Friction
                    currentVelocity -= currentVelocity * (slideFriction * deltaTime);

                    // Downhill Acceleration
                    {
                        var acceleration = Vector3.ProjectOnPlane
                        (
                            -_motor.CharacterUp,
                            _motor.GroundingStatus.GroundNormal
                        ) * slideAcceleration;
                        currentVelocity += acceleration * deltaTime;
                    }

                    // Slide STEERING
                    {              
                        var steerForce = deltaTime * slideSteerAcceleration * movementDirection;
                        var targetVelocity = currentVelocity + steerForce;
                        targetVelocity = Vector3.ClampMagnitude(targetVelocity, currentVelocity.magnitude);
                        steerForce = _state.Acceleration = targetVelocity - currentVelocity;    // * CHECK LATER
                        currentVelocity += steerForce;
                    }

                    // Slide END
                    if (currentVelocity.magnitude < slideEndSpeed)
                    {
                        _state.CurrentAction = CharacterAction.Crouch;
                        _state.Stance = Stance.Crouch;
                    }
                }
                // Normal Grounded Movement
                else
                {
                    // Calculate Target Velocity
                    var targetSpeed = _state.Stance is Stance.Crouch
                        ? moveSpeed * crouchSpeedMultiplier 
                        : _requestedSprint
                            ? sprintSpeed
                            : moveSpeed;
                    var targetAcceleration = _state.Stance is Stance.Crouch
                        ? crouchAcceleration
                        : _requestedSprint
                            ? sprintAcceleration
                            : moveAcceleration;
                    var targetVelocity = targetSpeed * movementDirection;

                    // Update State Machine
                    if (_requestedMovement.sqrMagnitude > 0f)
                    {
                        if (_state.Stance is Stance.Stand)
                            _state.CurrentAction = targetSpeed == sprintSpeed ? CharacterAction.Sprint : CharacterAction.Move;
                        else if (_state.Stance is Stance.Crouch)
                            _state.CurrentAction = CharacterAction.CrouchMove;
                    }
                    else
                    {
                        _state.CurrentAction = _state.Stance is Stance.Crouch ? CharacterAction.Crouch : CharacterAction.Idle;
                    }

                    // Apply Movement
                    var moveVelocity = Vector3.Lerp
                    (
                        currentVelocity,
                        targetVelocity,
                        1f - Mathf.Exp(-targetAcceleration * deltaTime)
                    );
                    _state.Acceleration = (moveVelocity - currentVelocity) / deltaTime;
                    currentVelocity = moveVelocity;
                }
            }
            // Else, character is in the air...
            else
            {
                _timeSinceUngrounded += deltaTime;

                // Aerial Movement
                if (_requestedMovement.sqrMagnitude > 0f)
                {
                    var planarDirection = Vector3.ProjectOnPlane(_requestedMovement, Vector3.up);
                    var currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
                    var steerForce = airAcceleration * deltaTime * planarDirection;

                    var targetPlanarVelocity = currentPlanarVelocity + steerForce;

                    if (Vector3.Dot(planarDirection, currentPlanarVelocity) > 0f)
                    {
                        _steerTimer += deltaTime;
                        _steerTimer = Mathf.Clamp(_steerTimer, 0f, steerLimit);
                    }
                    else
                    {
                        _steerTimer -= deltaTime;
                        _steerTimer = Mathf.Clamp(_steerTimer, 0f, steerLimit);
                    }

                    steerForce = _steerTimer < steerLimit 
                                ? targetPlanarVelocity - currentPlanarVelocity
                                : Vector3.zero;

                    currentVelocity += steerForce;
                }

                // Apply Gravity
                currentVelocity.y += -effectiveGravity * deltaTime;
            }

            // Jump Action
            if (_requestedJump)
            {
                var canCoyoteJump = _timeSinceUngrounded < coyoteTime && !_ungroundedDueToJump;

                _requestedJump = false;
                _requestedCrouch = false;
                _requestedCrouchInAir = false;

                if (_state.Grounded || canCoyoteJump)
                {
                    _state.CurrentAction = CharacterAction.Jump;
                    _ungroundedDueToJump = true;

                    _motor.ForceUnground(0f);
                    var jumpSpeed = Mathf.Sqrt(-2 * jumpHeight * -effectiveGravity);
                    currentVelocity.y = currentVelocity.y < 0f ? 0f : currentVelocity.y;
                    currentVelocity += jumpSpeed * _motor.GroundingStatus.GroundNormal;
                }
                else
                {
                    _timeSinceJumpRequest += deltaTime;
                    _requestedJump = _timeSinceJumpRequest < coyoteTime;
                }
            }
        }
        // Reset Velocity if inputs disabled
        else
        {
            currentVelocity = Vector3.zero;
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // Update 'tempState'
        _tempState = _state;

        // Crouch
        if (_requestedCrouch && _state.Stance is Stance.Stand)
        {
            if (_state.Grounded)
            {
                _state.CurrentAction = CharacterAction.Crouch;
                _state.Stance = Stance.Crouch;
                _motor.SetCapsuleDimensions
                (
                    radius: _motor.Capsule.radius,
                    height: crouchingCapsuleHeight,
                    yOffset: crouchingCapsuleHeight * 0.5f
                );
            }
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // Uncrouch
        if (!_requestedCrouch && _state.Stance is not Stance.Stand)
        {
            // Tentatively "stand up" the character capsule
            _motor.SetCapsuleDimensions(
                radius: _motor.Capsule.radius,
                height: standingCapsuleHeight,
                yOffset: standingCapsuleHeight * 0.5f
            );

            // Check for collider overlap above character
            var pos = _motor.TransientPosition;
            var rot = _motor.TransientRotation;
            var mask = _motor.CollidableLayers;
            if (_motor.CharacterOverlap(pos, rot, _overlapCollisions, mask, QueryTriggerInteraction.Ignore) > 0)
            {
                // Re-crouch
                _requestedCrouch = true;
                _motor.SetCapsuleDimensions(
                    radius: _motor.Capsule.radius,
                    height: crouchingCapsuleHeight,
                    yOffset: crouchingCapsuleHeight * 0.5f
                );
            }
            else
            {
                _state.CurrentAction = _state.CurrentAction is CharacterAction.Jump
                                        ? CharacterAction.Jump
                                        : CharacterAction.Idle;
                _state.Stance = Stance.Stand;
            }
        }

        // Update State Machine
        _prevState = _tempState;
        _state.Grounded = _motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = _motor.Velocity;
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;

    public void OnDiscreteCollisionDetected(Collider hitCollider) {}

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {}

    public void PostGroundingUpdate(float deltaTime) {}

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) {}   

    #region HELPER FUNCTIONS
    // Returns desired camera position
    public Transform GetCameraPosition() => cameraPosition;

    // Toggles player input
    private void EnableInput()  {  _inputEnabled = true;   }
    private void DisableInput() {  _inputEnabled = false;  }

    // State Machine Getters
    public CharacterState GetState() => _state;
    public CharacterState GetPrevState() => _prevState;

    // Returns raw movement input
    public Vector3 GetRawDirectionalMovement() => _requestedMovementRaw;

    //  Returns true/false if character is eligible to start sliding
    private bool CanSlide()
    {
        bool isMoving = _requestedMovement.sqrMagnitude > 0f;               // needs to be moving *
        bool isFastEnough = _state.Velocity.magnitude >= slideMinSpeed; // needs to move @ a certain speed *
        bool isCrouching = _state.Stance is Stance.Crouch;                  // needs to be crouching *
        bool wasStanding = _prevState.Stance is Stance.Stand;               // was previously standing
        bool wasAirborne = !_prevState.Grounded;                            // was previosuly in the air

        return isMoving && isFastEnough && isCrouching && (wasStanding || wasAirborne);
    }

    //  Returns true/false if character is eligible to sprint
    private bool CanSprint(Vector3 currentVelocity)
    {
        bool isMovingFoward = Vector3.Dot(_motor.CharacterForward, _requestedMovement) > 0f;    // needs to be moving foward *
        bool isStanding = _state.Stance is Stance.Stand;                                        // is standing
        bool isMovingFastEnough = currentVelocity.magnitude >= moveSpeed;                       // is moving at a certain speed
        return _requestedSprint && isMovingFoward && (isStanding || isMovingFastEnough);
    }
    #endregion
}
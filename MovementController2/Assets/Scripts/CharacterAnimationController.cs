using UnityEngine;
public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private Animator animator;

    private CharacterState _prevState;

    private static readonly int action = Animator.StringToHash("CurrentAction");
    private static readonly int stance = Animator.StringToHash("Stance");

    void Start()
    {
        animator.SetInteger(stance, (int)playerCharacter.GetState().Stance);
    } 

    void Update()
    {
        // Feed Player Input
        var input = playerCharacter.GetRawDirectionalMovement();
        animator.SetFloat("xInput", input.x);
        animator.SetFloat("yInput", input.y);

        var currentState = playerCharacter.GetState();

        // Feed Current Stance (only if changed)
        if (currentState.Stance != _prevState.Stance)
        {
            animator.SetInteger(stance, (int)currentState.Stance);
        }

        // Feed Current Action (only if changed)
        if (currentState.CurrentAction != _prevState.CurrentAction)
        {
            animator.SetInteger(action, (int)currentState.CurrentAction);
            Debug.Log(currentState.CurrentAction);
        }

        // Feed Grounding Stats
        if (currentState.Grounded != _prevState.Grounded)
        {
            animator.SetBool("IsGrounded", currentState.Grounded);
        }
        
        _prevState = currentState;
    }
}

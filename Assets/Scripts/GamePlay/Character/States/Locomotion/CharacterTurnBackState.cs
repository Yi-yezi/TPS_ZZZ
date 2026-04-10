using UnityEngine;

/// <summary>
/// 转身状态。
/// </summary>
public class CharacterTurnBackState : StateBase<LocomotionStateId>
{
    private readonly CharacterBehaviour owner;
    private Vector3 targetDirection;

    public CharacterTurnBackState(CharacterBehaviour owner) : base(LocomotionStateId.TurnBack)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        targetDirection = owner.GetMoveDirection();
        owner.Animator.SetTrigger(AnimParam.TurnBack);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }

    public override void Update(float deltaTime)
    {
        if (targetDirection.sqrMagnitude > 0.01f)
        {
            owner.FaceDirection(targetDirection, deltaTime);
        }
    }
}

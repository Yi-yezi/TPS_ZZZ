using UnityEngine;

/// <summary>
/// 起步状态（Idle → Walk 过渡）。
/// </summary>
public class CharacterWalkStartState : StateBase<LocomotionStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterWalkStartState(CharacterBehaviour owner) : base(LocomotionStateId.WalkStart)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.WalkStart);
    }

    public override void Update(float deltaTime)
    {
        Vector3 direction = owner.GetMoveDirection();
        owner.RotateTowards(direction, deltaTime);
    }
}

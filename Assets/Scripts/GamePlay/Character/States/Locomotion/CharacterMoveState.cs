using UnityEngine;

/// <summary>
/// 移动状态（Walk / Run 由 BlendTree 按 Speed 参数混合）。
/// </summary>
public class CharacterMoveState : StateBase<LocomotionStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterMoveState(CharacterBehaviour owner) : base(LocomotionStateId.Move)
    {
        this.owner = owner;
    }

    public override void Update(float deltaTime)
    {
        Vector3 direction = owner.GetMoveDirection();
        owner.RotateTowards(direction, deltaTime);
        owner.Animator.SetFloat(AnimParam.Speed, owner.MoveInputMagnitude);
    }
}

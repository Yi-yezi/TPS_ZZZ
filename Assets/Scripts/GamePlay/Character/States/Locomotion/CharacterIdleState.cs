/// <summary>
/// 站立状态。
/// </summary>
public class CharacterIdleState : StateBase<LocomotionStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterIdleState(CharacterBehaviour owner) : base(LocomotionStateId.Idle)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }

    public override void Update(float deltaTime)
    {
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

/// <summary>
/// 死亡状态。
/// </summary>
public class CharacterDeadState : StateBase<CharacterStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterDeadState(CharacterBehaviour owner) : base(CharacterStateId.Dead)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.Dead);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

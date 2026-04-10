/// <summary>
/// 受击状态。
/// </summary>
public class CharacterHitState : StateBase<CharacterStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterHitState(CharacterBehaviour owner) : base(CharacterStateId.Hit)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.Hit);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

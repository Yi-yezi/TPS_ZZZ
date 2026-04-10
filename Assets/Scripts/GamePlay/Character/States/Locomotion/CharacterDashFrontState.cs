/// <summary>
/// 前冲状态。
/// </summary>
public class CharacterDashFrontState : StateBase<DashStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterDashFrontState(CharacterBehaviour owner) : base(DashStateId.DashFront)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.DashFront);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

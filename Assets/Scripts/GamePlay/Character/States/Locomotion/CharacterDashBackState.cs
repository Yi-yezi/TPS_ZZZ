/// <summary>
/// 后闪状态。
/// </summary>
public class CharacterDashBackState : StateBase<DashStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterDashBackState(CharacterBehaviour owner) : base(DashStateId.DashBack)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.DashBack);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

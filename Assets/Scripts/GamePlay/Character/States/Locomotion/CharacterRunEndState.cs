/// <summary>
/// 跑步结束（急停）状态。
/// </summary>
public class CharacterRunEndState : StateBase<LocomotionStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterRunEndState(CharacterBehaviour owner) : base(LocomotionStateId.RunEnd)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.RunEnd);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

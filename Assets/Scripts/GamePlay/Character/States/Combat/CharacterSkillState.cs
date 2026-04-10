
/// <summary>
/// 技能Q状态。
/// </summary>
public class CharacterSkillState : StateBase<CombatStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterSkillState(CharacterBehaviour owner) : base(CombatStateId.SkillQ)
    {
        this.owner = owner;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        owner.Animator.SetTrigger(AnimParam.SkillQ);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }
}

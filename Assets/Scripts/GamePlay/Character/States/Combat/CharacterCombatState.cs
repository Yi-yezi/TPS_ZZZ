/// <summary>
/// Combat 子状态机：NormalAttack1/2/3 / SkillQ。
/// </summary>
public enum CombatStateId
{
    NormalAttack1,
    NormalAttack2,
    NormalAttack3,
    SkillQ,
}

public class CharacterCombatState : StateMachine<CombatStateId>
{
    #region 字段

    private readonly CharacterBehaviour owner;
    private CombatStateId entrySubId = CombatStateId.NormalAttack1;

    #endregion

    #region 初始化

    public CharacterCombatState(CharacterBehaviour owner)
    {
        this.owner = owner;
        defaultId = CombatStateId.NormalAttack1;
        states[CombatStateId.NormalAttack1] = new CharacterNormalAttack1State(owner);
        states[CombatStateId.NormalAttack2] = new CharacterNormalAttack2State(owner);
        states[CombatStateId.NormalAttack3] = new CharacterNormalAttack3State(owner);
        states[CombatStateId.SkillQ]        = new CharacterSkillState(owner);
    }

    #endregion

    #region 状态回调

    /// <summary>设置进入 Combat 时的初始子状态。</summary>
    public void SetEntryState(CombatStateId id)
    {
        entrySubId = id;
    }

    public override void OnEnter()
    {
        defaultId = entrySubId;
        base.OnEnter();
    }

    public override void Update(float deltaTime)
    {
        EvaluateSubTransitions();
        base.Update(deltaTime);
    }

    #endregion

    #region 子状态转换

    private void EvaluateSubTransitions()
    {
        if (!CurrentState.IsFinished)
            return;

        if (CurrentState is CharacterComboAttackBase combo && combo.ComboRequested && combo.NextComboId.HasValue)
        {
            ChangeState(combo.NextComboId.Value);
            return;
        }

        // 子状态结束且无连击 → 退出 Combat，回到 Locomotion
        owner.CharacterState.RequestState(CharacterStateId.Locomotion);
    }

    #endregion
}

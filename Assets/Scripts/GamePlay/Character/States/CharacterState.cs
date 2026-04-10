using Input;

public enum CharacterStateId
{
    Locomotion,
    Combat,
    Hit,
    Dead,
}

/// <summary>
/// 顶层状态机：Locomotion / Combat / Hit / Dead。
/// </summary>
public class CharacterState : StateMachine<CharacterStateId>
{
    #region 字段

    private readonly CharacterBehaviour owner;

    private readonly CharacterLocomotionState locomotionState;
    private readonly CharacterCombatState combatState;
    private readonly CharacterHitState hitState;
    private readonly CharacterDeadState deadState;

    private bool hasPendingState;
    private CharacterStateId pendingState;

    #endregion


    #region 初始化

    public CharacterState(CharacterBehaviour owner)
    {
        this.owner = owner;
        defaultId = CharacterStateId.Locomotion;

        locomotionState = new CharacterLocomotionState(owner);
        combatState = new CharacterCombatState(owner);
        hitState = new CharacterHitState(owner);
        deadState = new CharacterDeadState(owner);

        states[CharacterStateId.Locomotion] = locomotionState;
        states[CharacterStateId.Combat]     = combatState;
        states[CharacterStateId.Hit]        = hitState;
        states[CharacterStateId.Dead]       = deadState;
    }

    #endregion

    #region 状态回调

    public override void Update(float deltaTime)
    {
        EvaluateTransitions();
        base.Update(deltaTime);
        ApplyPendingState();
    }

    #endregion

    #region 顶层状态转换

    private void EvaluateTransitions()
    {
        if (owner.InputHandler == null)
            return;

        if (currentId == CharacterStateId.Dead)
            return;

        // Hit 完成 → Locomotion
        if (currentId == CharacterStateId.Hit)
        {
            if (hitState.IsFinished)
                ChangeState(CharacterStateId.Locomotion);
            return;
        }

        // Combat 内部自行管理退出
        if (currentId == CharacterStateId.Combat)
            return;

        // Locomotion → 检测战斗输入
        if (currentId == CharacterStateId.Locomotion)
        {
            // Dash 中不可发起攻击
            if (locomotionState.CurrentStateId == LocomotionStateId.Dash)
                return;

            InputFrame frame = owner.InputHandler.CurrentFrame;

            if (frame.IsPressed(InputButton.NormalAttack))
            {
                combatState.SetEntryState(CombatStateId.NormalAttack1);
                ChangeState(CharacterStateId.Combat);
                return;
            }

            if (frame.IsPressed(InputButton.SkillQ))
            {
                combatState.SetEntryState(CombatStateId.SkillQ);
                ChangeState(CharacterStateId.Combat);
                return;
            }
        }
    }

    /// <summary>
    /// 延迟切换顶层状态（下一次 Update 生效）。
    /// </summary>
    public void RequestState(CharacterStateId id)
    {
        hasPendingState = true;
        pendingState = id;
    }

    private void ApplyPendingState()
    {
        if (!hasPendingState)
            return;

        hasPendingState = false;
        ChangeState(pendingState);
    }

    #endregion
}


/// <summary>
/// 连击攻击状态基类。连击窗口基于动画 normalizedTime，结束由 SMB 驱动。
/// </summary>
public abstract class CharacterComboAttackBase : StateBase<CombatStateId>
{
    #region 字段与属性

    protected readonly CharacterBehaviour owner;
    private readonly int triggerHash;
    private readonly float comboWindowStart;
    private bool comboRequested;

    /// <summary>是否在连击窗口内按下了攻击键。</summary>
    public bool ComboRequested => comboRequested;

    /// <summary>下一段连击的状态 ID（null 表示终结段）。</summary>
    public CombatStateId? NextComboId { get; }

    #endregion

    #region 初始化

    protected CharacterComboAttackBase(
        CharacterBehaviour owner,
        CombatStateId id,
        int triggerHash,
        CombatStateId? nextComboId,
        float comboWindowStart = 0.6f)
        : base(id)
    {
        this.owner = owner;
        this.triggerHash = triggerHash;
        this.comboWindowStart = comboWindowStart;
        NextComboId = nextComboId;
    }

    #endregion

    #region 状态回调

    public override void OnEnter()
    {
        base.OnEnter();
        comboRequested = false;
        owner.Animator.SetTrigger(triggerHash);
        owner.Animator.SetFloat(AnimParam.Speed, 0f);
    }

    public override void Update(float deltaTime)
    {
        if (!comboRequested && NextComboId.HasValue)
        {
            var stateInfo = owner.Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= comboWindowStart)
            {
                var frame = owner.InputHandler.CurrentFrame;
                if (frame.IsPressed(Input.InputButton.NormalAttack))
                {
                    comboRequested = true;
                }
            }
        }
    }

    #endregion
}

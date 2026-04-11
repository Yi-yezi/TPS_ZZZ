namespace SkillSystem
{
    /// <summary>
    /// 输入指令类型
    /// </summary>
    public enum EInputCommand
    {
        None = 0,
        Move = 1,
        Dodge = 2,
        Attack = 3,
        Skill = 4,
        Ultimate = 5,
        SwitchCharacter = 6,
        DodgeFront = 7,   // 有移动输入时的 Dodge 指令
        DodgeBack  = 8,   // 无移动输入时的 Dodge 指令
    }

    /// <summary>
    /// 输入阶段
    /// </summary>
    public enum EInputPhase
    {
        Down,
        Press,
        Up,
    }

    /// <summary>
    /// 攻击/技能类型
    /// </summary>
    public enum EAttackStyle
    {
        NormalAttack,
        Skill,
        FinishSkill,
        SwitchSkill,
        DodgeAttack,
        BackDodgeAttack,
    }
}

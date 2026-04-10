using UnityEngine;

/// <summary>
/// 角色 Animator 参数名集中管理，使用预计算哈希避免运行时字符串查找。
/// </summary>
public static class AnimParam
{
    public static readonly int Speed   = Animator.StringToHash("Speed");
    public static readonly int Attack1 = Animator.StringToHash("Attack1");
    public static readonly int Attack2 = Animator.StringToHash("Attack2");
    public static readonly int Attack3 = Animator.StringToHash("Attack3");
    public static readonly int SkillQ  = Animator.StringToHash("SkillQ");
    public static readonly int DashFront = Animator.StringToHash("DashFront");
    public static readonly int DashBack  = Animator.StringToHash("DashBack");
    public static readonly int Hit     = Animator.StringToHash("Hit");
    public static readonly int Dead    = Animator.StringToHash("Dead");
    public static readonly int RunEnd    = Animator.StringToHash("RunEnd");
    public static readonly int TurnBack  = Animator.StringToHash("TurnBack");
    public static readonly int WalkStart = Animator.StringToHash("WalkStart");
}


/// <summary>
/// 普攻第二段。
/// </summary>
public class CharacterNormalAttack2State : CharacterComboAttackBase
{
    public CharacterNormalAttack2State(CharacterBehaviour owner)
        : base(owner, CombatStateId.NormalAttack2,
               AnimParam.Attack2,
               CombatStateId.NormalAttack3) { }
}

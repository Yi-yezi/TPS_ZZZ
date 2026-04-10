
/// <summary>
/// 普攻第一段。
/// </summary>
public class CharacterNormalAttack1State : CharacterComboAttackBase
{
    public CharacterNormalAttack1State(CharacterBehaviour owner)
        : base(owner, CombatStateId.NormalAttack1,
               AnimParam.Attack1,
               CombatStateId.NormalAttack2) { }
}

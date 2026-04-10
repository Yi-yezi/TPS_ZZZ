
/// <summary>
/// 普攻第三段（终结段，无后续连击）。
/// </summary>
public class CharacterNormalAttack3State : CharacterComboAttackBase
{
    public CharacterNormalAttack3State(CharacterBehaviour owner)
        : base(owner, CombatStateId.NormalAttack3,
               AnimParam.Attack3,
               null) { }
}

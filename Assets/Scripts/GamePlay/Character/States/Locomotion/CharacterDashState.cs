public enum DashStateId
{
    DashFront,
    DashBack,
}

/// <summary>
/// 冲刺子状态机：DashFront / DashBack。
/// </summary>
public class CharacterDashState : StateMachine<DashStateId>
{
    private readonly CharacterBehaviour owner;

    public CharacterDashState(CharacterBehaviour owner)
    {
        this.owner = owner;
        defaultId = DashStateId.DashFront;
        states[DashStateId.DashFront] = new CharacterDashFrontState(owner);
        states[DashStateId.DashBack]  = new CharacterDashBackState(owner);
    }

    public override void OnEnter()
    {
        var frame = owner.InputHandler.CurrentFrame;
        bool isBackward = false;
        if (frame.HasMovement)
        {
            var moveDir = owner.GetMoveDirection();
            float dot = UnityEngine.Vector3.Dot(owner.transform.forward, moveDir);
            isBackward = dot < -0.5f;
        }
        defaultId = isBackward ? DashStateId.DashBack : DashStateId.DashFront;
        base.OnEnter();
    }
}

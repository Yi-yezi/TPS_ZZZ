using UnityEngine;

/// <summary>
/// Locomotion 子状态机：Idle / WalkStart / Move / RunEnd / TurnBack / Dash。
/// </summary>
public enum LocomotionStateId
{
    Idle,
    WalkStart,
    Move,
    RunEnd,
    TurnBack,
    Dash,
}

public class CharacterLocomotionState : StateMachine<LocomotionStateId>
{
    #region 字段

    private readonly CharacterBehaviour owner;
    private const float RunEndSpeedThreshold = 0.5f;
    private const float TurnAngleThreshold = 120f;

    private bool wasRunning;

    #endregion

    #region 初始化

    public CharacterLocomotionState(CharacterBehaviour owner)
    {
        this.owner = owner;
        defaultId = LocomotionStateId.Idle;
        states[LocomotionStateId.Idle]      = new CharacterIdleState(owner);
        states[LocomotionStateId.WalkStart] = new CharacterWalkStartState(owner);
        states[LocomotionStateId.Move]      = new CharacterMoveState(owner);
        states[LocomotionStateId.RunEnd]    = new CharacterRunEndState(owner);
        states[LocomotionStateId.TurnBack]  = new CharacterTurnBackState(owner);
        states[LocomotionStateId.Dash]      = new CharacterDashState(owner);
    }

    #endregion

    #region 状态回调

    public override void OnEnter()
    {
        wasRunning = false;
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
        var frame = owner.InputHandler.CurrentFrame;

        // Dash 最高优先
        if (frame.IsPressed(Input.InputButton.Dash) &&
            currentId != LocomotionStateId.Dash)
        {
            ChangeState(LocomotionStateId.Dash);
            return;
        }

        // 有限时长子状态播完后回各自的后续状态
        if (currentId == LocomotionStateId.Dash ||
            currentId == LocomotionStateId.RunEnd ||
            currentId == LocomotionStateId.TurnBack)
        {
            if (CurrentState.IsFinished)
                ChangeState(LocomotionStateId.Idle);
            return;
        }

        if (currentId == LocomotionStateId.WalkStart)
        {
            if (CurrentState.IsFinished)
                ChangeState(LocomotionStateId.Move);
            return;
        }

        if (frame.HasMovement)
        {
            // 检测急转身
            if (currentId == LocomotionStateId.Move)
            {
                Vector3 moveDir = owner.GetMoveDirection();
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    float angle = Vector3.Angle(owner.transform.forward, moveDir);
                    if (angle > TurnAngleThreshold)
                    {
                        ChangeState(LocomotionStateId.TurnBack);
                        return;
                    }
                }
            }

            wasRunning = owner.MoveInputMagnitude > RunEndSpeedThreshold;

            // 从 Idle 起步 → WalkStart
            if (currentId == LocomotionStateId.Idle)
                ChangeState(LocomotionStateId.WalkStart);
            else
                ChangeState(LocomotionStateId.Move);
        }
        else
        {
            // 从跑步突然松手 → RunEnd
            if (currentId == LocomotionStateId.Move && wasRunning)
            {
                wasRunning = false;
                ChangeState(LocomotionStateId.RunEnd);
            }
            else
            {
                ChangeState(LocomotionStateId.Idle);
            }
        }
    }

    #endregion
}

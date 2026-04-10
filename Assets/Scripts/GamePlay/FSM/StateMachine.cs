using System.Collections.Generic;

/// <summary>
/// 通用状态机，实现 IState 以支持嵌套（HFSM）。
/// TId 为状态标识枚举类型。
/// </summary>
public class StateMachine<TId> : IState where TId : struct
{
    protected readonly Dictionary<TId, IState> states = new();
    protected TId currentId;
    protected TId defaultId;

    /// <summary>当前激活的子状态。</summary>
    public IState CurrentState { get; private set; }

    /// <summary>当前状态标识。</summary>
    public TId CurrentStateId => currentId;

    public bool IsFinished { get; protected set; }

    public IState CurrentSubState => CurrentState;

    public void MarkFinished() => IsFinished = true;

    /// <summary>添加一个状态到状态机。</summary>
    public void AddState(TId id, IState state) => states[id] = state;

    public virtual void OnEnter()
    {
        IsFinished = false;
        ChangeState(defaultId);
    }

    public virtual void OnExit()
    {
        CurrentState?.OnExit();
        CurrentState = null;
    }

    public virtual void Update(float deltaTime)
    {
        CurrentState?.Update(deltaTime);
    }

    public virtual void FixedUpdate(float fixedDeltaTime)
    {
        CurrentState?.FixedUpdate(fixedDeltaTime);
    }

    /// <summary>切换到指定状态。</summary>
    public void ChangeState(TId id)
    {
        if (EqualityComparer<TId>.Default.Equals(currentId, id) && CurrentState != null)
            return;

        UnityEngine.Debug.Log($"[FSM] {GetType().Name}: {currentId} → {id}");

        CurrentState?.OnExit();
        currentId = id;
        CurrentState = states[id];
        CurrentState?.OnEnter();
    }
}

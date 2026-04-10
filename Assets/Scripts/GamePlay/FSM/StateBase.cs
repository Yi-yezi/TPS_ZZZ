/// <summary>
/// 通用状态基类，不绑定特定拥有者。TId 为状态标识枚举类型。
/// </summary>
public abstract class StateBase<TId> : IState where TId : struct
{
    public TId Id { get; }
    public bool IsFinished { get; protected set; }

    protected StateBase(TId id)
    {
        Id = id;
    }

    public void MarkFinished() => IsFinished = true;
    public virtual IState CurrentSubState => null;

    public virtual void OnEnter() { IsFinished = false; }
    public virtual void OnExit() { }
    public virtual void Update(float deltaTime) { }
    public virtual void FixedUpdate(float fixedDeltaTime) { }
}

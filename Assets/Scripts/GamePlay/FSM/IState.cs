/// <summary>
/// 状态机状态接口。
/// </summary>
public interface IState
{
    void OnEnter();
    void OnExit();
    void Update(float deltaTime);
    void FixedUpdate(float fixedDeltaTime);

    /// <summary>状态是否已完成。</summary>
    bool IsFinished { get; }

    /// <summary>由外部（如 SMB）标记状态完成。</summary>
    void MarkFinished();

    /// <summary>若包含子状态机则返回当前子状态，否则返回 null。</summary>
    IState CurrentSubState { get; }
}

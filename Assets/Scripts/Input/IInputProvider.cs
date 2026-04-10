namespace Input
{
    /// <summary>
    /// 输入数据源接口。
    /// 游戏逻辑（CharacterBehaviour 等）只依赖此接口，不直接接触 Unity Input System。
    ///
    /// 后续帧同步扩展路径：
    ///   本地  → InputHandler : IInputProvider          （当前实现，本文件同目录）
    ///   网络  → NetworkInputProvider : IInputProvider  （接收对端发来的 InputFrame）
    ///   回放  → ReplayInputProvider  : IInputProvider  （按帧索引返回录制数据）
    ///
    /// 切换数据源时，CharacterBehaviour 无需任何修改，只需挂载不同的 IInputProvider 实现。
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// 当前帧快照。调用方须先调用 Tick()（若实现类需要）再读取此属性。
        /// </summary>
        InputFrame CurrentFrame { get; }

        /// <summary>是否允许输入。false 时 CurrentFrame 始终返回 InputFrame.Empty。</summary>
        bool IsEnabled { get; set; }
    }
}

namespace BehaviorTree
{
    /// <summary>
    /// 行为树节点的执行状态
    /// </summary>
    public enum NodeStatus
    {
        /// <summary>节点执行成功</summary>
        Success,
        /// <summary>节点执行失败</summary>
        Failure,
        /// <summary>节点仍在执行中（需要下一帧继续）</summary>
        Running,
    }
}

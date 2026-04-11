namespace BehaviorTree
{
    /// <summary>
    /// 行为树节点抽象基类
    /// </summary>
    public abstract class BehaviorTreeNode
    {
        /// <summary>
        /// 每帧由行为树驱动调用，返回当前执行状态
        /// </summary>
        public abstract NodeStatus Evaluate();
    }
}

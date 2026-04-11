using System;

namespace BehaviorTree
{
    /// <summary>
    /// 行动节点：包装一个 Func&lt;NodeStatus&gt;，直接返回其状态。
    /// </summary>
    public sealed class ActionNode : BehaviorTreeNode
    {
        private readonly Func<NodeStatus> action;

        public ActionNode(Func<NodeStatus> action)
        {
            this.action = action;
        }

        public override NodeStatus Evaluate()
        {
            return action();
        }
    }
}

using System;

namespace BehaviorTree
{
    /// <summary>
    /// 条件节点：包装一个 Func&lt;bool&gt;，返回 true 则 Success，否则 Failure。
    /// </summary>
    public sealed class ConditionNode : BehaviorTreeNode
    {
        private readonly Func<bool> condition;

        public ConditionNode(Func<bool> condition)
        {
            this.condition = condition;
        }

        public override NodeStatus Evaluate()
        {
            return condition() ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}

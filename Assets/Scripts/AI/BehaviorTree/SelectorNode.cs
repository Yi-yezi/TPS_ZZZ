namespace BehaviorTree
{
    /// <summary>
    /// 选择节点（OR）：从左到右依次评估子节点，
    /// 遇到第一个非 Failure 状态则停止并返回该状态；全部失败则返回 Failure。
    /// </summary>
    public sealed class SelectorNode : BehaviorTreeNode
    {
        private readonly BehaviorTreeNode[] children;

        public SelectorNode(params BehaviorTreeNode[] children)
        {
            this.children = children;
        }

        public override NodeStatus Evaluate()
        {
            foreach (var child in children)
            {
                var status = child.Evaluate();
                if (status != NodeStatus.Failure)
                    return status;
            }
            return NodeStatus.Failure;
        }
    }
}

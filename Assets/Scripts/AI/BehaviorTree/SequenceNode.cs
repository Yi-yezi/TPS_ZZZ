namespace BehaviorTree
{
    /// <summary>
    /// 序列节点（AND）：从左到右依次评估子节点，
    /// 遇到第一个非 Success 状态则停止并返回该状态；全部成功则返回 Success。
    /// </summary>
    public sealed class SequenceNode : BehaviorTreeNode
    {
        private readonly BehaviorTreeNode[] children;

        public SequenceNode(params BehaviorTreeNode[] children)
        {
            this.children = children;
        }

        public override NodeStatus Evaluate()
        {
            foreach (var child in children)
            {
                var status = child.Evaluate();
                if (status != NodeStatus.Success)
                    return status;
            }
            return NodeStatus.Success;
        }
    }
}

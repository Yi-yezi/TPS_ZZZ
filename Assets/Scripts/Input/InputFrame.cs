using UnityEngine;

namespace Input
{
    /// <summary>
    /// 单逻辑帧输入快照（约 10 字节）。
    ///
    /// 设计目标：
    ///   - 所有字段为值类型，结构体可直接网络序列化 / 录像存储。
    ///   - MoveX/MoveY 使用定点数（×0.001f），避免跨端浮点精度差异。
    ///   - 派生属性（MoveVector、HasMovement）仅在本地运行时使用，不参与同步。
    ///
    /// 使用方式：
    ///   InputFrame frame = inputProvider.CurrentFrame;
    ///   if (frame.IsPressed(InputButton.Jump))   { ... }   // 仅本帧刚按下
    ///   if (frame.IsHeld(InputButton.Dash))       { ... }   // 持续按住中
    ///   float jumpForce = Mathf.Lerp(min, max, frame.JumpHeldTicks / (float)maxTicks);
    /// </summary>
    [System.Serializable]
    public readonly struct InputFrame
    {
        // ── 序列化字段（网络/录像传输的原始数据）────────────────────

        /// <summary>水平移动方向，定点数 [-1000, 1000] 对应 [-1f, 1f]。</summary>
        public readonly short MoveX;

        /// <summary>垂直移动方向，定点数 [-1000, 1000] 对应 [-1f, 1f]。</summary>
        public readonly short MoveY;

        /// <summary>
        /// 本帧发生 0→1 边沿的按钮集合（单帧，下帧自动消失）。
        /// 例：Jump = 跳跃键刚按下；MovementTap = 短按松开确认；MovementHoldStart = Hold 首帧。
        /// </summary>
        public readonly InputButton Pressed;

        /// <summary>当前帧持续处于按住状态的按钮集合（包含刚按下的按钮）。</summary>
        public readonly InputButton Held;

        /// <summary>Jump 键当前连续按住的帧数；松开后为 0。供跳跃高度控制使用。</summary>
        public readonly byte JumpHeldTicks;

        /// <summary>移动键当前连续按住的帧数；无移动输入时为 0。</summary>
        public readonly byte MovementHeldTicks;

        // ── 运行时派生属性（不参与同步判定）────────────────────────

        /// <summary>浮点移动向量，由定点数还原。</summary>
        public Vector2 MoveVector => new Vector2(MoveX * 0.001f, MoveY * 0.001f);

        /// <summary>本帧是否有移动输入。</summary>
        public bool HasMovement => MoveX != 0 || MoveY != 0;

        // ── 查询方法 ─────────────────────────────────────────────────

        /// <summary>查询某按钮本帧是否有 Pressed（0→1 边沿）事件。</summary>
        public bool IsPressed(InputButton btn) => (Pressed & btn) != 0;

        /// <summary>查询某按钮当前是否处于 Held（持续按住）状态。</summary>
        public bool IsHeld(InputButton btn) => (Held & btn) != 0;

        // ── 构造与空值 ────────────────────────────────────────────────

        /// <summary>空帧：所有输入为零/无。</summary>
        public static readonly InputFrame Empty = default;

        public InputFrame(
            short mx, short my,
            InputButton pressed, InputButton held,
            byte jumpHeldTicks, byte movementHeldTicks)
        {
            MoveX             = mx;
            MoveY             = my;
            Pressed           = pressed;
            Held              = held;
            JumpHeldTicks     = jumpHeldTicks;
            MovementHeldTicks = movementHeldTicks;
        }
    }
}

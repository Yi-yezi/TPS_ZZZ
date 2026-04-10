namespace Input
{
    /// <summary>
    /// 输入按钮位标志，用于 InputFrame 的 Pressed / Held 字段。
    /// 值类型（ushort），可直接网络序列化。
    ///
    /// Pressed：本帧发生 0→1 边沿的按钮。
    /// Held   ：当前帧持续按住的按钮（包含刚按下的）。
    /// </summary>
    [System.Flags]
    public enum InputButton : ushort
    {
        None              = 0,
        Jump              = 1 << 0,
        Dash              = 1 << 1,
        Crouch            = 1 << 2,
        NormalAttack      = 1 << 3,
        SkillQ            = 1 << 4,
        SkillE            = 1 << 5,
        /// <summary>短按移动后松开（完整 Tap 在松开帧确认）。</summary>
        MovementTap       = 1 << 6,
        /// <summary>移动按住帧数首次越过 holdMinTicks 的那一帧。</summary>
        MovementHoldStart = 1 << 7,
        /// <summary>本帧移动输入归零。</summary>
        MovementReleased  = 1 << 8,
        /// <summary>移动当前处于 Hold 状态（heldTicks >= holdMinTicks，持续帧）。</summary>
        MovementHeld      = 1 << 9,
    }
}

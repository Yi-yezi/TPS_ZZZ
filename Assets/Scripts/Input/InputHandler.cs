using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    /// <summary>
    /// 本地输入采集器，实现 IInputProvider。
    ///
    /// 职责：
    ///   - 订阅 Unity Input System 回调，维护原始输入状态。
    ///   - 每帧由游戏逻辑调用 Tick()，将原始状态快照为 InputFrame。
    ///   - 向上层暴露 CurrentFrame，游戏逻辑通过 IInputProvider 读取，不直接接触 Input System。
    ///
    /// 帧同步设计要点：
    ///   - Tap/Hold 检测基于「帧计数」，不依赖 Time.unscaledTime，保证确定性。
    ///   - Tick() 由 CharacterBehaviour.Update() 在帧首显式调用，时序明确可控。
    ///   - 事件（OnMovementTap 等）仅供表现层（动画触发、音效）订阅，不参与游戏逻辑。
    ///
    /// 后续扩展：添加 NetworkInputProvider / ReplayInputProvider 实现 IInputProvider，
    ///            CharacterBehaviour 无需任何修改即可切换数据源。
    /// </summary>
    public class InputHandler : MonoBehaviour, IInputProvider
    {
        [Header("输入配置")]
        [SerializeField] private bool enableInput = true;

        [Header("Tap / Hold 阈值（帧数）")]
        [Tooltip("按下后在此帧数内松开则判定为 Tap（默认 12 ≈ 0.2 秒 @60fps）")]
        [SerializeField] private byte tapMaxTicks  = 12;
        [Tooltip("按下后持续超过此帧数则首次触发 Hold（默认 12 ≈ 0.2 秒 @60fps）")]
        [SerializeField] private byte holdMinTicks = 12;

        private CharacterInput inputActions;

        // ── IInputProvider 实现 ───────────────────────────────────────
        private InputFrame currentFrame;

        /// <inheritdoc/>
        public InputFrame CurrentFrame => currentFrame;

        /// <inheritdoc/>
        public bool IsEnabled
        {
            get => enableInput;
            set { enableInput = value; if (!value) FlushAllRawState(); }
        }

        // ── 原始输入状态（由 Input System 回调写入，Tick() 时读取）────
        private Vector2 rawMoveInput;
        private bool    jumpHeld;
        private bool    dashHeld;
        private bool    crouchHeld;

        // 单帧下降沿 flag（Tick() 消费后由 ClearOneFrameFlags() 清零）
        private bool jumpDown;
        private bool dashDown;
        private bool crouchDown;
        private bool attackDown;
        private bool skillQDown;
        private bool skillEDown;

        // 移动方向暂存（供 Tap 事件使用）
        private Vector2 tapDirection;

        // 帧间状态
        private bool moveWasActive;
        private byte moveHeldTicks;
        private byte jumpHeldTicks;

        // ── 表现层事件（不应用于游戏逻辑判定）──────────────────────────

        /// <summary>短按移动后松开时触发（方向为松开前最后一帧的输入方向）。</summary>
        public event Action<Vector2> OnMovementTap;

        /// <summary>移动按住帧数首次越过 holdMinTicks 时触发。</summary>
        public event Action<Vector2> OnMovementHoldStart;

        /// <summary>移动输入归零时触发。</summary>
        public event Action OnMovementReleased;

        /// <summary>Jump 键刚按下时触发。</summary>
        public event Action OnJumpDown;

        // ── Unity 生命周期 ────────────────────────────────────────────
        private void Awake()    => InitializeInput();
        private void OnEnable() => inputActions?.Enable();
        private void OnDisable()=> inputActions?.Disable();
        private void OnDestroy(){ UnsubscribeAll(); inputActions?.Dispose(); }

        // ── 公共接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 生成本帧 InputFrame 快照。
        /// 必须在读取 CurrentFrame 之前调用，由 CharacterBehaviour.Update() 在帧首负责。
        /// </summary>
        public void Tick()
        {
            if (!enableInput)
            {
                currentFrame = InputFrame.Empty;
                ClearOneFrameFlags();
                return;
            }

            bool moveActive      = rawMoveInput.sqrMagnitude > 0.0001f;
            bool moveJustStarted = moveActive  && !moveWasActive;
            bool moveJustStopped = !moveActive && moveWasActive;

            // ── 移动帧计数 ────────────────────────────────────────────
            if (moveJustStarted)
                moveHeldTicks = 0;

            if (moveActive)
                moveHeldTicks = moveHeldTicks < byte.MaxValue
                    ? (byte)(moveHeldTicks + 1) : byte.MaxValue;

            // ── Hold 首帧检测 ─────────────────────────────────────────
            bool moveHoldStart = moveActive && (moveHeldTicks == holdMinTicks);

            // ── Tap 确认（松开时，且按住时间 <= tapMaxTicks，且未进入 Hold）──
            bool moveTap = false;
            if (moveJustStopped)
            {
                if (moveHeldTicks > 0 && moveHeldTicks <= tapMaxTicks)
                {
                    moveTap = true;
                    OnMovementTap?.Invoke(tapDirection);
                }
                OnMovementReleased?.Invoke();
                moveHeldTicks = 0; // 松开后归零
            }

            // ── Jump 帧计数 ───────────────────────────────────────────
            if (jumpHeld)
                jumpHeldTicks = jumpHeldTicks < byte.MaxValue
                    ? (byte)(jumpHeldTicks + 1) : byte.MaxValue;
            else
                jumpHeldTicks = 0;

            // ── 通知表现层事件 ────────────────────────────────────────
            if (moveHoldStart)
                OnMovementHoldStart?.Invoke(rawMoveInput);

            // ── 组装 Pressed bits（单帧边沿）─────────────────────────
            InputButton pressed = InputButton.None;
            if (jumpDown)        pressed |= InputButton.Jump;
            if (dashDown)        pressed |= InputButton.Dash;
            if (crouchDown)      pressed |= InputButton.Crouch;
            if (attackDown)      pressed |= InputButton.NormalAttack;
            if (skillQDown)      pressed |= InputButton.SkillQ;
            if (skillEDown)      pressed |= InputButton.SkillE;
            if (moveTap)         pressed |= InputButton.MovementTap;
            if (moveHoldStart)   pressed |= InputButton.MovementHoldStart;
            if (moveJustStopped) pressed |= InputButton.MovementReleased;

            // ── 组装 Held bits（持续状态）────────────────────────────
            InputButton held = InputButton.None;
            if (jumpHeld)   held |= InputButton.Jump;
            if (dashHeld)   held |= InputButton.Dash;
            if (crouchHeld) held |= InputButton.Crouch;
            if (moveActive && moveHeldTicks >= holdMinTicks)
                held |= InputButton.MovementHeld;

            // ── 量化移动向量（定点数，确保跨端一致性）───────────────
            short mx = (short)Mathf.Clamp(Mathf.RoundToInt(rawMoveInput.x * 1000f), -1000, 1000);
            short my = (short)Mathf.Clamp(Mathf.RoundToInt(rawMoveInput.y * 1000f), -1000, 1000);

            // ── 生成快照 ──────────────────────────────────────────────
            currentFrame = new InputFrame(
                mx, my,
                pressed, held,
                jumpHeldTicks,
                moveActive ? moveHeldTicks : (byte)0);

            // ── 更新帧间状态，清除单帧 flag ───────────────────────────
            moveWasActive = moveActive;
            ClearOneFrameFlags();

            
        }

        // ── 私有方法 ──────────────────────────────────────────────────

        private void ClearOneFrameFlags()
        {
            jumpDown = dashDown = crouchDown = attackDown = skillQDown = skillEDown = false;
        }

        private void FlushAllRawState()
        {
            rawMoveInput  = Vector2.zero;
            tapDirection  = Vector2.zero;
            jumpHeld      = dashHeld = crouchHeld = false;
            jumpHeldTicks = moveHeldTicks = 0;
            moveWasActive = false;
            ClearOneFrameFlags();
            currentFrame  = InputFrame.Empty;
        }

        private void InitializeInput()
        {
            inputActions = new CharacterInput();

            inputActions.Player.Movement.performed     += OnMovementPerformed;
            inputActions.Player.Movement.canceled      += OnMovementCanceled;
            inputActions.Player.Jump.performed         += OnJumpPerformed;
            inputActions.Player.Jump.canceled          += OnJumpCanceled;
            inputActions.Player.Dash.performed         += OnDashPerformed;
            inputActions.Player.Dash.canceled          += OnDashCanceled;
            inputActions.Player.Crouch.performed       += OnCrouchPerformed;
            inputActions.Player.Crouch.canceled        += OnCrouchCanceled;
            inputActions.Player.NormalAttack.performed += OnNormalAttackPerformed;
            inputActions.Player.SkillQ.performed       += OnSkillQPerformed;
            inputActions.Player.SkillE.performed       += OnSkillEPerformed;
        }

        private void UnsubscribeAll()
        {
            if (inputActions == null) return;

            inputActions.Player.Movement.performed     -= OnMovementPerformed;
            inputActions.Player.Movement.canceled      -= OnMovementCanceled;
            inputActions.Player.Jump.performed         -= OnJumpPerformed;
            inputActions.Player.Jump.canceled          -= OnJumpCanceled;
            inputActions.Player.Dash.performed         -= OnDashPerformed;
            inputActions.Player.Dash.canceled          -= OnDashCanceled;
            inputActions.Player.Crouch.performed       -= OnCrouchPerformed;
            inputActions.Player.Crouch.canceled        -= OnCrouchCanceled;
            inputActions.Player.NormalAttack.performed -= OnNormalAttackPerformed;
            inputActions.Player.SkillQ.performed       -= OnSkillQPerformed;
            inputActions.Player.SkillE.performed       -= OnSkillEPerformed;
        }

        // ── Input System 回调（仅更新原始状态，不含任何游戏逻辑）────

        private void OnMovementPerformed(InputAction.CallbackContext ctx)
            => rawMoveInput = ctx.ReadValue<Vector2>();

        private void OnMovementCanceled(InputAction.CallbackContext ctx)
        {
            tapDirection = rawMoveInput; // 保存方向供 Tick() 中 Tap 事件使用
            rawMoveInput = Vector2.zero;
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            if (!jumpHeld) { jumpDown = true; OnJumpDown?.Invoke(); }
            jumpHeld = true;
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx)   => jumpHeld   = false;
        private void OnDashPerformed(InputAction.CallbackContext ctx)   { dashDown = true; dashHeld = true; }
        private void OnDashCanceled(InputAction.CallbackContext ctx)    => dashHeld   = false;
        private void OnCrouchPerformed(InputAction.CallbackContext ctx) { crouchDown = true; crouchHeld = true; }
        private void OnCrouchCanceled(InputAction.CallbackContext ctx)  => crouchHeld = false;
        private void OnNormalAttackPerformed(InputAction.CallbackContext ctx) => attackDown = true;
        private void OnSkillQPerformed(InputAction.CallbackContext ctx)       => skillQDown = true;
        private void OnSkillEPerformed(InputAction.CallbackContext ctx)       => skillEDown = true;
    }
}

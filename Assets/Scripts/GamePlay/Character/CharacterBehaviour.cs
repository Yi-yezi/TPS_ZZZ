using UnityEngine;
using SkillSystem;

/// <summary>
/// 角色行为控制器
/// 所有状态（Idle/Move/Attack/Hit/Dead…）全部由 ActionSO 转移关系驱动
/// Animator 只负责播放对应 State，不连线
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CombatEntity))]
[RequireComponent(typeof(CharacterController))]
public class CharacterBehaviour : MonoBehaviour
{
    [Header("动作配置")]
    public ActionListSO actionList;

    [Header("重力")]
    public float gravity = -15f;

    [Header("转向")]
    [Tooltip("SmoothDamp 平滑时间（越小转向越快）")]
    public float rotationTime = 0.08f;

    [Header("锟定")] 
    [Tooltip("进入该范围自动锁定敌人")]
    public float lockOnRadius = 8f;
    [Tooltip("超出该范围自动解除锁定（应大于 lockOnRadius）")]
    public float lockOffRadius = 10f;

    // ─── Components ───
    private Animator animator;
    private CombatEntity combatEntity;
    private CharacterController characterController;

    // ─── Systems ───
    private ActionDriver actionDriver;
    private CharacterInput inputActions;
    private Camera mainCamera;

    // ─── State ───
    private Vector2 moveInput;
    private Vector3 _stickWorldDir;   // 每帧缓存，摇杆相对相机的世界方向
    private float verticalVelocity;
    private float _rotationVelocity;

    // ─── Blackboard ───
    public Blackboard Blackboard { get; } = new Blackboard();

    // ─── Lock-On State ───
    private bool _isLockedOn;
    private bool IsLockedOn => _isLockedOn && Blackboard.Has(BlackboardKeys.Target);

    // ═══════════════════════════════════════
    //   生命周期
    // ═══════════════════════════════════════

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combatEntity = GetComponent<CombatEntity>();
        characterController = GetComponent<CharacterController>();

        // 初始化动作驱动器
        actionDriver = new ActionDriver();
        actionDriver.Init(actionList);

        actionDriver.OnPlayAnimation += HandlePlayAnimation;
        actionDriver.OnVfxEvent += HandleVfxEvent;
        actionDriver.OnSfxEvent += HandleSfxEvent;
        actionDriver.OnHitFeelEvent += HandleHitFeelEvent;
        actionDriver.OnHitBoxCheck   += HandleHitBoxCheck;

        // 战斗事件
        combatEntity.OnDamaged += HandleDamaged;
        combatEntity.OnDied    += HandleDied;

        // 输入
        inputActions = new CharacterInput();
    }

    private void Start()
    {
        mainCamera = Camera.main;

        // 自动绑定最近的敌人作为攻击目标（可被外部 Lock-On 系统覆盖）
        var enemyObj = GameObject.FindWithTag("Enemy");
        if (enemyObj != null)
            Blackboard.Set(BlackboardKeys.Target, enemyObj.transform);

        // 初始进入 Idle 动作
        actionDriver.PlayAction(ActionNames.Idle);
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();
    private void OnDestroy()
    {
        combatEntity.OnDamaged -= HandleDamaged;
        combatEntity.OnDied    -= HandleDied;
        inputActions.Dispose();
    }

    private bool _wasMoving;

    private void Update()
    {
        ReadInput();

        if (combatEntity.IsDead) return;

        actionDriver.Update(Time.deltaTime);

        UpdateLockOn();

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        // 每帧计算一次摇杆世界方向，供朝向与位移共用
        _stickWorldDir = Vector3.zero;
        if (isMoving)
        {
            Vector3 camFwd   = mainCamera.transform.forward; camFwd.y = 0f; camFwd.Normalize();
            Vector3 camRight = mainCamera.transform.right;   camRight.y = 0f; camRight.Normalize();
            _stickWorldDir = (camFwd * moveInput.y + camRight * moveInput.x).normalized;
        }

        AlignToCameraDirection();
        FaceTargetWhenAttacking();

        // 持续转发移动指令，让 Idle→WalkStart / Move 等转移窗口能匹配
        if (isMoving)
        {
            actionDriver.SendCommand(EInputCommand.Move, EInputPhase.Press);
        }
        else if (_wasMoving)
            actionDriver.SendCommand(EInputCommand.Move, EInputPhase.Up);

        _wasMoving = isMoving;
    }

    // ═══════════════════════════════════════
    //   输入读取
    // ═══════════════════════════════════════

    private void ReadInput()
    {
        moveInput = inputActions.Player.Movement.ReadValue<Vector2>();

        if (inputActions.Player.Attack.WasPressedThisFrame())
            HandleCommand(EInputCommand.Attack, EInputPhase.Down);

        if (inputActions.Player.Dodge.WasPressedThisFrame())
        {
            // 有移动输入 → DodgeFront；无移动输入 → DodgeBack
            var dodgeCmd = moveInput.sqrMagnitude > 0.01f
                ? EInputCommand.DodgeFront
                : EInputCommand.DodgeBack;
            HandleCommand(dodgeCmd, EInputPhase.Down);
        }

        if (inputActions.Player.Skill.WasPressedThisFrame())
            HandleCommand(EInputCommand.Skill, EInputPhase.Down);

        if (inputActions.Player.UltimateSkill.WasPressedThisFrame())
            HandleCommand(EInputCommand.Ultimate, EInputPhase.Down);
    }

    private void HandleCommand(EInputCommand command, EInputPhase phase)
    {
        if (combatEntity.IsDead) return;

        // 所有指令都交给 ActionDriver，由当前动作的转移窗口决定是否匹配
        actionDriver.SendCommand(command, phase);
    }

    // ═══════════════════════════════════════
    //   转向
    // ═══════════════════════════════════════

    /// <summary>
    /// 每帧检查敌人距离，自动切换锁定状态。
    /// </summary>
    private void UpdateLockOn()
    {
        if (!Blackboard.TryGet<Transform>(BlackboardKeys.Target, out var target)) return;

        float dist = Vector3.Distance(transform.position, target.position);

        if (!_isLockedOn && dist <= lockOnRadius)
            _isLockedOn = true;
        else if (_isLockedOn && dist > lockOffRadius)
            _isLockedOn = false;
    }

    /// <summary>
    /// 有移动输入时朝向摇杆世界方向，停止后平滑回归相机朝向。
    /// </summary>
    private void AlignToCameraDirection()
    {
        float targetAngle;

        if (_stickWorldDir.sqrMagnitude > 0.001f)
        {
            // 移动中：朝向摇杆世界方向
            targetAngle = Mathf.Atan2(_stickWorldDir.x, _stickWorldDir.z) * Mathf.Rad2Deg;
        }
        else
        {
            // 停止时：回归相机水平朝向
            Vector3 camForward = mainCamera.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.001f) return;
            targetAngle = Mathf.Atan2(camForward.x, camForward.z) * Mathf.Rad2Deg;
        }

        float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
                                ref _rotationVelocity, rotationTime);
        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    /// <summary>
    /// 锁定敌人时，攻击动作期间强制朝向敌人（覆盖 AlignToCameraDirection 的结果）。
    /// 移动动作不受影响。
    /// </summary>
    private void FaceTargetWhenAttacking()
    {
        if (!IsLockedOn) return;

        var current = actionDriver.CurrentActionName;
        if (current == null) return;

        // 只在主动攻击动作期间朝向敌人（AttackRush / Attack_Normal_xx / SkillQ 等）
        bool isAttack = current.StartsWith("Attack") || current.StartsWith("Skill");
        if (!isAttack) return;

        var target = Blackboard.Get<Transform>(BlackboardKeys.Target);
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;

        // 瞬间对齐朝向（攻击已开始，不需要平滑）
        transform.rotation = Quaternion.LookRotation(toTarget);
    }

    // ═══════════════════════════════════════
    //   Root Motion + 重力
    // ═══════════════════════════════════════

    /// <summary>
    /// 所有位移 / 旋转由动画 Root Motion 驱动，叠加重力
    /// </summary>
    private void OnAnimatorMove()
    {
        Vector3 rootDelta = animator.deltaPosition;

        // 有移动输入时，将 Root Motion 的 XZ 位移重定向至摇杆世界方向（支持左右后退）
        if (_stickWorldDir.sqrMagnitude > 0.001f)
        {
            float xzMag = new Vector3(rootDelta.x, 0f, rootDelta.z).magnitude;
            rootDelta.x = _stickWorldDir.x * xzMag;
            rootDelta.z = _stickWorldDir.z * xzMag;
        }

        // 当前动作标记 disableGravity 时（如跳跃技能），Y 轴完全由 Root Motion 控制
        bool gravityDisabled = actionDriver.CurrentAction?.SourceAction.disableGravity ?? false;
        if (gravityDisabled)
        {
            verticalVelocity = 0f;
        }
        else
        {
            if (characterController.isGrounded)
                verticalVelocity = -0.5f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            rootDelta.y += verticalVelocity * Time.deltaTime;
        }

        characterController.Move(rootDelta);
    }

    // ═══════════════════════════════════════
    //   ActionDriver 回调
    // ═══════════════════════════════════════

    private void HandlePlayAnimation(string stateName, float fadeDuration)
    {
        animator.CrossFadeInFixedTime(stateName, fadeDuration);
    }

    private void HandleVfxEvent(ActionVfxEventData data)
    {
        if (data.VfxPrefab == null) return;

        Transform parent = null;
        if (!string.IsNullOrEmpty(data.ParentPath))
            parent = transform.FindDeepChild(data.ParentPath);

        Vector3 pos = (parent != null ? parent.position : transform.position) + data.PositionOffset;
        Quaternion rot = Quaternion.Euler(data.RotationOffset);

        var vfx = Instantiate(data.VfxPrefab, pos, rot);
        if (data.AttachToParent && parent != null)
            vfx.transform.SetParent(parent);

        Destroy(vfx, data.Duration > 0 ? data.Duration : 3f);
    }

    private void HandleSfxEvent(ActionSfxEventData data)
    {
        if (data.AudioClips == null || data.AudioClips.Length == 0) return;

        var clip = data.AudioClips[UnityEngine.Random.Range(0, data.AudioClips.Length)];
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, transform.position, data.Volume);
    }

    private void HandleHitFeelEvent(ActionHitFeelEventData data)
    {
        // TODO: 实现顿帧 + 屏幕震动
        // 顿帧: Time.timeScale = 0 → 协程等待 pauseFrameDuration → 恢复
        // 震动: 相机 shake (shakeForce, shakeDuration)
    }

    private void HandleHitBoxCheck(ActionHitBoxEventData data)
    {
        if (!Blackboard.TryGet<Transform>(BlackboardKeys.Target, out var target)) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > data.AttackDistance) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f &&
            Vector3.Angle(transform.forward, toTarget) > data.AttackAngle)
            return;

        var targetCombatEntity = target.GetComponent<CombatEntity>();
        if (targetCombatEntity == null) return;

        // 命中→先消耗判定盒，这帧内不再再次检测
        actionDriver.ConsumeHitBox(data);

        targetCombatEntity.TakeDamage(new DamageInfo
        {
            Damage       = data.Damage,
            Attacker     = gameObject,
            HitPoint     = target.position,
            HitDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward,
        });
    }

    // ═══════════════════════════════════════
    //   战斗事件
    // ═══════════════════════════════════════

    [Header("受击参数")]
    [Tooltip("单次伤害达到此值则触发重型受击信号")]
    public float heavyHitDamageThreshold = 30f;

    /// <summary>
    /// 受到伤害：发送受击信号到 ActionDriver。
    /// 当前 ActionSO 若配置了对应 signalTransition（如 Idle/Move 配置了 HitLight→HitFront）
    /// 则被打断进入受击动作；没有配置的动作（翻滚无敌帧、特定技能等）自动无视。
    /// </summary>
    private void HandleDamaged(DamageInfo info)
    {
        if (combatEntity.IsDead) return;

        bool isHeavy   = info.Damage >= heavyHitDamageThreshold;
        bool fromFront = Vector3.Dot(transform.forward, -info.HitDirection) >= 0f;

        string signal = (isHeavy, fromFront) switch
        {
            (true,  true)  => HitSignals.HitHeavyFront,
            (true,  false) => HitSignals.HitHeavyBack,
            (false, true)  => HitSignals.HitLightFront,
            (false, false) => HitSignals.HitLightBack,
        };

        actionDriver.SendSignal(signal);
    }

    private void HandleDied()
    {
        // 死亡强制推倒，无视当前动作
        actionDriver.ForceAction(ActionNames.Dead, 0.1f);
        enabled = false;
    }

    // ═══════════════════════════════════════
    //   工具
    // ═══════════════════════════════════════

}

/// <summary>
/// Transform 扩展 - 深度查找子物体
/// </summary>
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}

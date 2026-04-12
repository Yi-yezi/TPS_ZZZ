using BehaviorTree;
using SkillSystem;
using UnityEngine;

/// <summary>
/// 怪物行为控制器
/// 动作播放由 ActionDriver 驱动；AI 决策由行为树驱动；移动由 Root Motion 驱动。
/// 挂载要求：Animator、CombatEntity、CharacterController
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CombatEntity))]
[RequireComponent(typeof(CharacterController))]
public class MonsterBehaviour : MonoBehaviour
{
    [Header("动作配置")]
    public ActionListSO actionList;

    [Header("感知")]
    [Tooltip("能发现玩家的最大距离")]
    public float detectRadius = 10f;
    [Tooltip("能发起攻击的最大距离")]
    public float attackRadius = 2f;

    [Header("物理")]
    public float gravity = -15f;

    [Header("战斗")]
    [Tooltip("两次攻击之间的最短间隔（秒）")]
    public float attackCooldown = 2f;
    [Tooltip("旋转速度（度/秒）")]
    public float rotationSpeed = 180f;

    [Header("受击")]
    [Tooltip("触发重型受击动作的最低单次伤害")]
    public float heavyHitDamageThreshold = 30f;

    [Tooltip("受击音效列表，随机播放（通过对象池在受击点播放）")]
    public AudioClip[] hitSfxClips;
    [Range(0f, 1f)] public float hitSfxVolume = 0.7f;

    // ─── Components ───
    private Animator animator;
    private CombatEntity combatEntity;
    private CharacterController characterController;
    private AudioSource audioSource;
    private float verticalVelocity;

    // ─── Systems ───
    private ActionDriver actionDriver;

    // ─── Blackboard ───
    public Blackboard Blackboard { get; } = new Blackboard();

    // ─── AI State ───
    private BehaviorTreeNode behaviorTreeRoot;
    private Transform TargetTransform       => Blackboard.Get<Transform>(BlackboardKeys.Target);
    private CombatEntity TargetCombatEntity => Blackboard.Get<CombatEntity>(BlackboardKeys.TargetCombatEntity);
    private float lastAttackTime = float.MinValue;

    // ═══════════════════════════════════════
    //   生命周期
    // ═══════════════════════════════════════

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combatEntity = GetComponent<CombatEntity>();
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        // Root Motion 在 OnAnimatorMove 中手动应用
        animator.applyRootMotion = false;

        actionDriver = new ActionDriver();
        actionDriver.Init(actionList);
        actionDriver.OnPlayAnimation += HandlePlayAnimation;
        actionDriver.OnVfxEvent     += HandleVfxEvent;
        actionDriver.OnSfxEvent     += HandleSfxEvent;
        actionDriver.OnHitBoxCheck   += HandleHitBoxCheck;

        combatEntity.OnDamaged += HandleDamaged;
        combatEntity.OnDied    += HandleDied;
    }

    private void Start()
    {
        actionDriver.PlayAction(MonsterActionNames.Idle);
        behaviorTreeRoot = BuildBehaviorTree();
    }

    private void OnDestroy()
    {
        combatEntity.OnDamaged -= HandleDamaged;
        combatEntity.OnDied    -= HandleDied;
    }

    private void Update()
    {
        // ActionDriver 始终更新（包括受击动画期间）
        actionDriver.Update(Time.deltaTime);

        if (combatEntity.IsDead || IsHitReacting) return;

        // 目标为空或已死，重新扫描范围内存活玩家
        var current = TargetCombatEntity;
        if (current == null || current.IsDead)
            UpdateTarget();

        behaviorTreeRoot.Evaluate();
    }

    /// <summary>
    /// 在 detectRadius 内扫描所有存活玩家，选取最近的一个锁定。
    /// 多次被调用时只在目标失效后才执行，开销可接受。
    /// </summary>
    private void UpdateTarget()
    {
        float nearest = float.MaxValue;
        Transform bestTransform = null;
        CombatEntity bestEntity = null;

        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var entity = go.GetComponent<CombatEntity>();
            if (entity == null || entity.IsDead) continue;

            float dist = Vector3.Distance(transform.position, go.transform.position);
            if (dist <= detectRadius && dist < nearest)
            {
                nearest = dist;
                bestTransform = go.transform;
                bestEntity = entity;
            }
        }

        Blackboard.Set(BlackboardKeys.Target, bestTransform);
        Blackboard.Set(BlackboardKeys.TargetCombatEntity, bestEntity);
    }

    /// <summary>是否正处于受击动作（替代 isHitReacting 布尔）</summary>
    private bool IsHitReacting =>
        actionDriver.CurrentActionName is
            MonsterActionNames.HitFrontLight or
            MonsterActionNames.HitFrontHeavy or
            MonsterActionNames.HitStay;

    private void OnAnimatorMove()
    {
        Vector3 rootDelta = animator.deltaPosition;

        if (characterController.isGrounded)
            verticalVelocity = -0.5f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        rootDelta.y += verticalVelocity * Time.deltaTime;

        characterController.Move(rootDelta);
    }

    // ═══════════════════════════════════════
    //   行为树构建
    // ═══════════════════════════════════════

    /// <summary>
    /// 构建行为树结构：
    ///   Selector
    ///   ├── Action: 维持当前攻击（正在攻击时返回 Running，屏蔽其他分支）
    ///   ├── Sequence: 发起攻击（在攻击范围内且冷却已就绪）
    ///   │   ├── Condition: 玩家在攻击范围内
    ///   │   ├── Condition: 攻击冷却已就绪
    ///   │   └── Action: 发起随机攻击
    ///   ├── Sequence: 追击玩家（玩家在感知范围内）
    ///   │   ├── Condition: 玩家在感知范围内
    ///   │   └── Action: 追击逻辑
    ///   └── Action: 待机
    /// </summary>
    private BehaviorTreeNode BuildBehaviorTree()
    {
        return new SelectorNode(
            new ActionNode(MaintainCurrentAttack),
            new SequenceNode(
                new ConditionNode(IsPlayerInAttackRange),
                new ConditionNode(IsAttackCooledDown),
                new ActionNode(StartRandomAttack)
            ),
            new SequenceNode(
                new ConditionNode(IsPlayerInDetectRange),
                new ActionNode(ChasePlayer)
            ),
            new ActionNode(Idle)
        );
    }

    // ═══════════════════════════════════════
    //   条件
    // ═══════════════════════════════════════

    private bool IsPlayerInDetectRange()
    {
        if (TargetTransform == null) return false;
        if (TargetCombatEntity != null && TargetCombatEntity.IsDead) return false;
        return Vector3.Distance(transform.position, TargetTransform.position) <= detectRadius;
    }

    private bool IsPlayerInAttackRange()
    {
        if (TargetTransform == null) return false;
        if (TargetCombatEntity != null && TargetCombatEntity.IsDead) return false;
        return Vector3.Distance(transform.position, TargetTransform.position) <= attackRadius;
    }

    private bool IsAttackCooledDown()
    {
        return Time.time - lastAttackTime >= attackCooldown;
    }

    // ═══════════════════════════════════════
    //   行动节点
    // ═══════════════════════════════════════

    /// <summary>
    /// 正在攻击时返回 Running（阻止其他分支介入），否则返回 Failure。
    /// </summary>
    private NodeStatus MaintainCurrentAttack()
    {
        string currentActionName = actionDriver.CurrentActionName;
        bool isAttacking = currentActionName == MonsterActionNames.Attack01
                        || currentActionName == MonsterActionNames.Attack02;

        if (!isAttacking) return NodeStatus.Failure;

        var target = TargetTransform;
        if (target != null)
            RotateTowardsTarget(target.position);

        return NodeStatus.Running;
    }

    /// <summary>
    /// 随机选择 Attack01 或 Attack02 发起攻击，记录冷却时间。
    /// </summary>
    private NodeStatus StartRandomAttack()
    {
        var target = TargetTransform;
        if (target != null)
            RotateTowardsTarget(target.position);

        string attackActionName = Random.value < 0.5f
            ? MonsterActionNames.Attack01
            : MonsterActionNames.Attack02;

        actionDriver.PlayAction(attackActionName);
        lastAttackTime = Time.time;

        return NodeStatus.Success;
    }

    /// <summary>
    /// Root Motion 驱动追击：旋转朝向玩家，动画 Root Motion 自动向前推进角色。
    /// </summary>
    private NodeStatus ChasePlayer()
    {
        var target = TargetTransform;
        if (target == null) return NodeStatus.Failure;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        // 旋转朝向玩家，Root Motion 会将角色向其面朴方向推进
        RotateTowardsTarget(target.position);

        if (distanceToPlayer <= attackRadius)
        {
            // 已在攻击范围内，停止前进等待冷却
            PlayIfNotCurrent(MonsterActionNames.Idle);
        }
        else
        {
            PlayIfNotCurrent(MonsterActionNames.Run);
        }

        return NodeStatus.Running;
    }

    /// <summary>
    /// 停止移动，播放 Idle 动作。
    /// </summary>
    private NodeStatus Idle()
    {
        PlayIfNotCurrent(MonsterActionNames.Idle);
        return NodeStatus.Success;
    }

    // ═══════════════════════════════════════
    //   战斗事件
    // ═══════════════════════════════════════

    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (combatEntity.IsDead) return;

        // 受击音效 → 对象池（在受击点播放）
        if (hitSfxClips != null && hitSfxClips.Length > 0)
        {
            var clip = hitSfxClips[Random.Range(0, hitSfxClips.Length)];
            if (clip != null)
                EffectPoolManager.Instance.PlaySfx(clip, damageInfo.HitPoint, hitSfxVolume);
        }

        bool isHeavy   = damageInfo.Damage >= heavyHitDamageThreshold;
        bool fromFront = Vector3.Dot(transform.forward, -damageInfo.HitDirection) >= 0f;

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
        actionDriver.ForceAction(MonsterActionNames.Dead);
        enabled = false;
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

        Vector3 position = (parent != null ? parent.position : transform.position) + data.PositionOffset;
        Quaternion rotation = Quaternion.Euler(data.RotationOffset);

        Transform attachParent = (data.AttachToParent && parent != null) ? parent : null;
        EffectPoolManager.Instance.SpawnVfx(data.VfxPrefab, position, rotation, data.Duration, attachParent);
    }

    private void HandleSfxEvent(ActionSfxEventData data)
    {
        if (data.AudioClips == null || data.AudioClips.Length == 0) return;

        var clip = data.AudioClips[Random.Range(0, data.AudioClips.Length)];
        if (clip == null) return;

        audioSource.pitch = data.Pitch + Random.Range(-data.PitchRandomRange, data.PitchRandomRange);
        audioSource.PlayOneShot(clip, data.Volume);
    }

    private void HandleHitBoxCheck(ActionHitBoxEventData data)
    {
        var target = TargetTransform;
        if (target == null) return;

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
    //   工具
    // ═══════════════════════════════════════

    /// <summary>
    /// 仅当当前动作不是目标动作时才播放，避免重复触发。
    /// </summary>
    private void PlayIfNotCurrent(string actionName)
    {
        if (actionDriver.CurrentActionName != actionName)
            actionDriver.PlayAction(actionName);
    }

    /// <summary>
    /// 以固定旋转速度平滑转向目标位置（水平面）。
    /// </summary>
    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}

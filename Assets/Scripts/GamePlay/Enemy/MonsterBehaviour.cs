using System.Collections;
using System.Collections.Generic;
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

    // ─── Components ───
    private Animator animator;
    private CombatEntity combatEntity;
    private CharacterController characterController;
    private float verticalVelocity;

    // ─── Systems ───
    private ActionDriver actionDriver;

    // ─── HitBox Cache ───
    private readonly Dictionary<string, HitBox> hitBoxes = new();

    // ─── AI State ───
    private BehaviorTreeNode behaviorTreeRoot;
    private Transform playerTransform;
    private float lastAttackTime = float.MinValue;
    private bool isHitReacting;

    // ═══════════════════════════════════════
    //   生命周期
    // ═══════════════════════════════════════

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combatEntity = GetComponent<CombatEntity>();
        characterController = GetComponent<CharacterController>();

        // Root Motion 在 OnAnimatorMove 中手动应用
        animator.applyRootMotion = false;

        foreach (var hitBox in GetComponentsInChildren<HitBox>(true))
        {
            hitBoxes[hitBox.hitBoxName] = hitBox;
            hitBox.Deactivate();
        }

        actionDriver = new ActionDriver();
        actionDriver.Init(actionList);
        actionDriver.OnPlayAnimation += HandlePlayAnimation;
        actionDriver.OnVfxEvent     += HandleVfxEvent;
        actionDriver.OnSfxEvent     += HandleSfxEvent;
        actionDriver.OnHitBoxActivate   += HandleHitBoxActivate;
        actionDriver.OnHitBoxDeactivate += HandleHitBoxDeactivate;

        combatEntity.OnDamaged += HandleDamaged;
        combatEntity.OnDied    += HandleDied;
    }

    private void Start()
    {
        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            playerTransform = playerObject.transform;

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

        if (combatEntity.IsDead || isHitReacting) return;

        behaviorTreeRoot.Evaluate();
    }

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
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= detectRadius;
    }

    private bool IsPlayerInAttackRange()
    {
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= attackRadius;
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

        if (playerTransform != null)
            RotateTowardsTarget(playerTransform.position);

        return NodeStatus.Running;
    }

    /// <summary>
    /// 随机选择 Attack01 或 Attack02 发起攻击，记录冷却时间。
    /// </summary>
    private NodeStatus StartRandomAttack()
    {
        if (playerTransform != null)
            RotateTowardsTarget(playerTransform.position);

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
        if (playerTransform == null) return NodeStatus.Failure;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 旋转朝向玩家，Root Motion 会将角色向其面朝方向推进
        RotateTowardsTarget(playerTransform.position);

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
        StartCoroutine(PlayHitReaction(damageInfo));
    }

    private IEnumerator PlayHitReaction(DamageInfo damageInfo)
    {
        isHitReacting = true;

        string hitActionName = damageInfo.Damage >= heavyHitDamageThreshold
            ? MonsterActionNames.HitFrontHeavy
            : MonsterActionNames.HitFrontLight;

        actionDriver.ForceAction(hitActionName);
        float hitAnimationDuration = actionDriver.ActionDuration;

        yield return new WaitForSeconds(hitAnimationDuration);

        isHitReacting = false;
        actionDriver.PlayAction(MonsterActionNames.Idle);
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
        DeactivateAllHitBoxes();
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

        var vfxInstance = Object.Instantiate(data.VfxPrefab, position, rotation);
        if (data.AttachToParent && parent != null)
            vfxInstance.transform.SetParent(parent);

        Object.Destroy(vfxInstance, data.Duration > 0 ? data.Duration : 3f);
    }

    private void HandleSfxEvent(ActionSfxEventData data)
    {
        if (data.AudioClips == null || data.AudioClips.Length == 0) return;

        var clip = data.AudioClips[Random.Range(0, data.AudioClips.Length)];
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, transform.position, data.Volume);
    }

    private void HandleHitBoxActivate(ActionHitBoxEventData data)
    {
        if (!hitBoxes.TryGetValue(data.HitBoxName, out var hitBox)) return;
        hitBox.Activate(data.Damage, gameObject);
    }

    private void HandleHitBoxDeactivate(ActionHitBoxEventData data)
    {
        if (!hitBoxes.TryGetValue(data.HitBoxName, out var hitBox)) return;
        hitBox.Deactivate();
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

    private void DeactivateAllHitBoxes()
    {
        foreach (var hitBox in hitBoxes.Values)
            hitBox.Deactivate();
    }
}

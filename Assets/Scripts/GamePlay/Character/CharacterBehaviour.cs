using System;
using System.Collections.Generic;
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

    [Header("指令 → 入口动作（从待机状态触发）")]
    public List<CommandActionMapping> commandMappings = new();

    [Header("重力")]
    public float gravity = -15f;

    // ─── Components ───
    private Animator animator;
    private CombatEntity combatEntity;
    private CharacterController cc;

    // ─── Systems ───
    private ActionPlayer actionPlayer;
    private CharacterInput inputActions;

    // ─── State ───
    private Vector2 moveInput;
    private float verticalVelocity;

    // ─── HitBox Cache ───
    private readonly Dictionary<string, HitBox> hitBoxes = new();

    // ═══════════════════════════════════════
    //   生命周期
    // ═══════════════════════════════════════

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combatEntity = GetComponent<CombatEntity>();
        cc = GetComponent<CharacterController>();

        // 收集所有 HitBox
        foreach (var hb in GetComponentsInChildren<HitBox>(true))
        {
            hitBoxes[hb.hitBoxName] = hb;
            hb.Deactivate();
        }

        // 初始化动作驱动器
        actionPlayer = new ActionPlayer();
        actionPlayer.Init(actionList);

        actionPlayer.OnPlayAnimation += HandlePlayAnimation;
        actionPlayer.OnReturnToLocomotion += HandleReturnToLocomotion;
        actionPlayer.OnVfxEvent += HandleVfxEvent;
        actionPlayer.OnSfxEvent += HandleSfxEvent;
        actionPlayer.OnHitFeelEvent += HandleHitFeelEvent;

        // 战斗事件

        // 输入
        inputActions = new CharacterInput();
    }

    private void Start()
    {
        // 初始进入 Idle 动作
        actionPlayer.PlayAction(ActionNames.Idle);
    }

    private void OnEnable() => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();
    private void OnDestroy() => inputActions.Dispose();

    private void Update()
    {
        ReadInput();

        if (combatEntity.IsDead) return;

        actionPlayer.Update(Time.deltaTime);

        // 持续转发移动指令，让 Idle→Move / Move 保持等转移窗口能匹配
        if (moveInput.sqrMagnitude > 0.01f)
            actionPlayer.SendCommand(EInputCommand.Move, EInputPhase.Press);
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
            HandleCommand(EInputCommand.Dodge, EInputPhase.Down);

        if (inputActions.Player.Skill.WasPressedThisFrame())
            HandleCommand(EInputCommand.Skill, EInputPhase.Down);

        if (inputActions.Player.UltimateSkill.WasPressedThisFrame())
            HandleCommand(EInputCommand.Ultimate, EInputPhase.Down);
    }

    private void HandleCommand(EInputCommand command, EInputPhase phase)
    {
        if (combatEntity.IsDead) return;

        // 所有指令都交给 ActionPlayer，由当前动作的转移窗口决定是否匹配
        actionPlayer.SendCommand(command, phase);
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

        // 重力
        if (cc.isGrounded)
            verticalVelocity = -0.5f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        rootDelta.y += verticalVelocity * Time.deltaTime;

        cc.Move(rootDelta);
        transform.rotation *= animator.deltaRotation;
    }

    // ═══════════════════════════════════════
    //   ActionPlayer 回调
    // ═══════════════════════════════════════

    private void HandlePlayAnimation(string stateName, float fadeDuration)
    {
        animator.CrossFadeInFixedTime(stateName, fadeDuration);
    }

    private void HandleReturnToLocomotion()
    {
        DeactivateAllHitBoxes();
        // 回到 Idle 动作
        actionPlayer.PlayAction(ActionNames.Idle);
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

    // ═══════════════════════════════════════
    //   战斗事件
    // ═══════════════════════════════════════

    // ═══════════════════════════════════════
    //   工具
    // ═══════════════════════════════════════

    private void DeactivateAllHitBoxes()
    {
        foreach (var hb in hitBoxes.Values)
            hb.Deactivate();
    }

    /// <summary>
    /// 指令 → 入口动作映射（Inspector 配置）
    /// </summary>
    [Serializable]
    public class CommandActionMapping
    {
        public EInputCommand command;
        public string actionName;
    }
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

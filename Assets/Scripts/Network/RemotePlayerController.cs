using SkillSystem;
using UnityEngine;
using TpsProtocol;

/// <summary>
/// 远程玩家控制器。
///
/// 与本地玩家 CharacterBehaviour 的区别：
///   - 不处理输入、不做碰撞检测
///   - 动画使用 Root Motion 驱动位移（避免滑步），同时用服务器权威位置做漂移修正
///   - 动画/VFX/SFX 由 ActionDriver 本地驱动（收到 ActionName 变化时 PlayAction）
///   - HitBox 不订阅（远程玩家不做命中判定，由攻击方客户端判定后上报服务器）
/// </summary>
[RequireComponent(typeof(Animator))]
public class RemotePlayerController : MonoBehaviour
{
    [Header("动作配置")]
    public ActionListSO actionList;

    // ── 修正参数 ───
    [Header("位置修正")]
    [Tooltip("每秒将多少比例的漂移修正回服务器位置（0~1，越大越紧跟）")]
    public float correctionRate = 5f;
    [Tooltip("偏差超过此距离时直接传送（防止角色长距离漂移）")]
    public float snapDistance = 3f;

    [Header("旋转插值")]
    [Tooltip("旋转插值速度")]
    public float rotationLerpSpeed = 15f;

    // ── 组件引用 ───
    private Animator animator;
    private AudioSource audioSource;

    // ── 系统 ───
    private ActionDriver actionDriver;

    // ── 网络同步状态 ───
    private int playerId;
    private Vector3 serverPosition;     // 服务器权威位置
    private float targetRotationY;
    private string lastActionName = "";
    private float lastActionTime;       // 用于检测同名动作重播（ActionTime 回跳）
    private float hp;
    private float maxHp;

    // ── 公开属性 ───
    public int PlayerId => playerId;
    public float Hp => hp;
    public float MaxHp => maxHp;

    // ═══════════════════════════════════════
    //   初始化
    // ═══════════════════════════════════════

    public void Init(int id, float currentHp, float maxHp)
    {
        playerId = id;
        hp = currentHp;
        this.maxHp = maxHp;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true; // Root Motion 驱动位移，避免滑步

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    void Start()
    {
        // ActionDriver 在 Start 中初始化，确保 actionList 已由 SpawnRemotePlayer 赋值
        actionDriver = new ActionDriver();
        actionDriver.Init(actionList);
        actionDriver.OnPlayAnimation += HandlePlayAnimation;
        actionDriver.OnVfxEvent += HandleVfxEvent;
        actionDriver.OnSfxEvent += HandleSfxEvent;

        serverPosition = transform.position;
        targetRotationY = transform.eulerAngles.y;
        actionDriver.PlayAction("Idle");
    }

    void Update()
    {
        // 驱动 ActionDriver（按 Timeline 时间轴触发动画、VFX、SFX 事件）
        if (actionDriver != null)
            actionDriver.Update(Time.deltaTime);

        // 平滑插值旋转（处理 0°/360° 分界）
        float curY = transform.eulerAngles.y;
        float newY = Mathf.LerpAngle(curY, targetRotationY, Time.deltaTime * rotationLerpSpeed);
        transform.rotation = Quaternion.Euler(0, newY, 0);
    }

    /// <summary>
    /// Root Motion 驱动位移 + 服务器位置漂移修正。
    /// 动画自带的位移保证脚步不滑，同时缓慢修正累积误差。
    /// </summary>
    private void OnAnimatorMove()
    {
        // 1. 应用 Root Motion 位移
        Vector3 pos = transform.position + animator.deltaPosition;

        // 2. 计算与服务器位置的偏差
        Vector3 drift = serverPosition - pos;
        float driftDist = drift.magnitude;

        if (driftDist > snapDistance)
        {
            // 偏差过大 → 直接传送到服务器位置
            pos = serverPosition;
        }
        else if (driftDist > 0.01f)
        {
            // 每帧混合一部分修正量，让角色缓慢靠近服务器位置
            pos += drift * Mathf.Clamp01(correctionRate * Time.deltaTime);
        }

        transform.position = pos;
    }

    // ═══════════════════════════════════════
    //   网络数据接收
    // ═══════════════════════════════════════

    /// <summary>
    /// 由 NetworkManager 每次收到 S2C_StateSync 时调用（20Hz）。
    /// 更新插值目标 + HP，检测动作名变化时切换 ActionDriver。
    /// </summary>
    public void ApplySnapshot(PlayerSnapshot snapshot)
    {
        serverPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
        targetRotationY = snapshot.RotationY;
        hp = snapshot.Hp;

        // 同步动作：名称变化 或 同名动作 ActionTime 回跳（连续同一动作） → 重新播放
        if (!string.IsNullOrEmpty(snapshot.ActionName) && actionDriver != null)
        {
            bool nameChanged = snapshot.ActionName != lastActionName;
            bool restarted = !nameChanged && snapshot.ActionTime < lastActionTime - 0.05f;

            if (nameChanged || restarted)
            {
                lastActionName = snapshot.ActionName;
                actionDriver.PlayActionAtTime(snapshot.ActionName, snapshot.ActionTime);
                animator.CrossFadeInFixedTime(snapshot.ActionName, 0.1f, 0, snapshot.ActionTime);
            }
            lastActionTime = snapshot.ActionTime;
        }
    }

    /// <summary>
    /// 远程玩家死亡。
    /// </summary>
    public void OnDied()
    {
        if (actionDriver != null)
            actionDriver.ForceAction("Dead", 0.1f);
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

        Transform attachParent = (data.AttachToParent && parent != null) ? parent : null;
        EffectPoolManager.Instance.SpawnVfx(data.VfxPrefab, pos, rot, data.Duration, attachParent);
    }

    private void HandleSfxEvent(ActionSfxEventData data)
    {
        if (data.AudioClips == null || data.AudioClips.Length == 0) return;

        var clip = data.AudioClips[Random.Range(0, data.AudioClips.Length)];
        if (clip == null) return;

        audioSource.pitch = data.Pitch + Random.Range(-data.PitchRandomRange, data.PitchRandomRange);
        audioSource.PlayOneShot(clip, data.Volume);
    }
}

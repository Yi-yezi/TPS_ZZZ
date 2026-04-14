using SkillSystem;
using UnityEngine;
using TpsProtocol;

/// <summary>
/// 服务器驱动的怪物控制器。
/// 接收服务器 MonsterSnapshot 驱动位置/旋转/动作，Root Motion 防滑步。
/// 不运行本地 AI，HitBox 不订阅（伤害由服务器判定）。
/// </summary>
[RequireComponent(typeof(Animator))]
public class RemoteMonsterController : MonoBehaviour
{
    [Header("动作配置")]
    public ActionListSO actionList;

    [Header("位置修正")]
    public float correctionRate = 5f;
    public float snapDistance = 3f;

    [Header("旋转插值")]
    public float rotationLerpSpeed = 15f;

    // ── 组件引用 ───
    private Animator animator;
    private AudioSource audioSource;
    private CombatEntity combatEntity;

    // ── 系统 ───
    private ActionDriver actionDriver;

    // ── 网络同步状态 ───
    private int monsterId;
    private Vector3 serverPosition;
    private float targetRotationY;
    private string lastActionName = "";

    // ── 公开属性 ───
    public int MonsterId => monsterId;
    public float Hp => combatEntity != null ? combatEntity.CurrentHp : 0;
    public float MaxHp => combatEntity != null ? combatEntity.maxHp : 0;
    public bool IsDead => combatEntity != null ? combatEntity.IsDead : true;

    // ── 受击参数（从预制体读取）───
    [Header("受击音效")]
    public AudioClip[] hitSfxClips;
    [Range(0f, 1f)] public float hitSfxVolume = 0.7f;

    // ═══════════════════════════════════════
    //   初始化
    // ═══════════════════════════════════════

    public void Init(int id, float currentHp, float maxHp, CombatEntity combat)
    {
        monsterId = id;
        combatEntity = combat;
        if (combatEntity != null)
        {
            combatEntity.maxHp = maxHp;
            combatEntity.ResetHp();
            // 如果初始 HP 不是满血，用 TakeDamage 扣到正确值
            if (currentHp < maxHp)
                combatEntity.TakeDamage(new DamageInfo { Damage = maxHp - currentHp });
        }
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    void Start()
    {
        actionDriver = new ActionDriver();
        if (actionList != null)
        {
            actionDriver.Init(actionList);
            actionDriver.OnPlayAnimation += HandlePlayAnimation;
            actionDriver.OnVfxEvent += HandleVfxEvent;
            actionDriver.OnSfxEvent += HandleSfxEvent;
            actionDriver.PlayAction("Idle");
        }

        serverPosition = transform.position;
        targetRotationY = transform.eulerAngles.y;
    }

    void Update()
    {
        if (actionDriver != null)
            actionDriver.Update(Time.deltaTime);

        // 旋转插值
        float curY = transform.eulerAngles.y;
        float newY = Mathf.LerpAngle(curY, targetRotationY, Time.deltaTime * rotationLerpSpeed);
        transform.rotation = Quaternion.Euler(0, newY, 0);
    }

    private void OnAnimatorMove()
    {
        Vector3 pos = transform.position + animator.deltaPosition;

        Vector3 drift = serverPosition - pos;
        float driftDist = drift.magnitude;

        if (driftDist > snapDistance)
            pos = serverPosition;
        else if (driftDist > 0.01f)
            pos += drift * Mathf.Clamp01(correctionRate * Time.deltaTime);

        transform.position = pos;
    }

    // ═══════════════════════════════════════
    //   网络数据接收
    // ═══════════════════════════════════════

    public void ApplySnapshot(MonsterSnapshot snapshot)
    {
        if (IsDead) return; // 死亡后不再接受快照更新

        serverPosition = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
        targetRotationY = snapshot.RotationY;

        // 同步 HP 到 CombatEntity
        if (combatEntity != null && !Mathf.Approximately(snapshot.Hp, combatEntity.CurrentHp))
        {
            combatEntity.SetHp(snapshot.Hp, combatEntity.maxHp);
        }

        if (!string.IsNullOrEmpty(snapshot.ActionName) && snapshot.ActionName != lastActionName)
        {
            lastActionName = snapshot.ActionName;
            if (actionDriver != null)
            {
                actionDriver.PlayActionAtTime(snapshot.ActionName, snapshot.ActionTime);
                animator.CrossFadeInFixedTime(snapshot.ActionName, 0.1f, 0, snapshot.ActionTime);
            }
        }
    }

    /// <summary>
    /// 服务器广播怪物受伤时调用。
    /// hitSignal: 受击信号名（如 "HitLightFront"），发送给 ActionDriver。
    /// </summary>
    public void OnDamaged(float damage, Vector3 hitPoint, Vector3 hitDirection, string hitSignal)
    {
        // HP 由 ApplySnapshot 统一同步，此处不再重复扣血，只播放受击表现

        // 受击音效
        if (hitSfxClips != null && hitSfxClips.Length > 0)
        {
            var clip = hitSfxClips[Random.Range(0, hitSfxClips.Length)];
            if (clip != null && EffectPoolManager.Instance != null)
                EffectPoolManager.Instance.PlaySfx(clip, hitPoint, hitSfxVolume);
        }

        // 受击动作
        if (!string.IsNullOrEmpty(hitSignal) && actionDriver != null)
            actionDriver.SendSignal(hitSignal);
    }

    public void OnDied()
    {
        // 确保 HP 归零（DamageEvent 可能晚于 MonsterDied 到达）
        if (combatEntity != null)
            combatEntity.SetHp(0, combatEntity.maxHp);

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

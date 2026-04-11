using System;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;

/// <summary>
/// 动作驱动器 - 基于 ActionSO 转移关系驱动动作播放，不是传统FSM
/// 所有状态转移由编辑器中配置的 CommandTransition / FinishTransition / SignalTransition 决定
/// Animator 只负责播放对应 State，不连线
/// </summary>
public class ActionDriver
{
    private readonly Dictionary<string, ActionRuntimeData> actionDB = new();
    private readonly Dictionary<string, ActionSO> actionSODB = new();

    // ─── 当前动作状态 ───
    public ActionRuntimeData CurrentAction { get; private set; }
    public string CurrentActionName => CurrentAction?.AnimatorStateName;
    public float ActionTime { get; private set; } // 动画播放时间（秒）
    public float ActionDuration { get; private set; }
    public bool IsPlaying => CurrentAction != null;

    // ─── 输入缓冲 ───
    private EInputCommand bufferedCommand;
    private EInputPhase bufferedPhase;
    private float bufferTimestamp;
    private const float MaxBufferAge = 0.5f; // 输入缓冲最大有效时间

    // ─── 事件游标 ───
    private int vfxCursor, sfxCursor, hitFeelCursor, hitBoxCursor;

    // 已激活的 HitBox 事件，用于跟踪到期后关闭
    private readonly List<ActionHitBoxEventData> activeHitBoxEvents = new();

    // ─── 回调 ───
    /// <summary>播放动画 (stateName, fadeDuration)</summary>
    public event Action<string, float> OnPlayAnimation;
    public event Action<ActionVfxEventData> OnVfxEvent;
    public event Action<ActionSfxEventData> OnSfxEvent;
    public event Action<ActionHitFeelEventData> OnHitFeelEvent;
    /// <summary>判定盒激活：进入 HitBoxClip 时间段</summary>
    public event Action<ActionHitBoxEventData> OnHitBoxActivate;
    /// <summary>判定盒关闭：离开 HitBoxClip 时间段</summary>
    public event Action<ActionHitBoxEventData> OnHitBoxDeactivate;


    // ═══════════════════════════════════════
    //   初始化
    // ═══════════════════════════════════════

    public void Init(ActionListSO actionList)
    {
        actionDB.Clear();
        actionSODB.Clear();

        foreach (var actionSO in actionList.actions)
        {
            if (actionSO == null) continue;

            var runtimeData = ActionUnpacker.Unpack(actionSO);

            // 按 StartTime 排序，便于顺序游标遍历
            runtimeData.VfxEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            runtimeData.SfxEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            runtimeData.HitFeelEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            runtimeData.HitBoxEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            runtimeData.TransitionWindows.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            actionDB[actionSO.name] = runtimeData;
            actionSODB[actionSO.name] = actionSO;
        }
    }

    // ═══════════════════════════════════════
    //   播放控制
    // ═══════════════════════════════════════

    /// <summary>播放指定动作</summary>
    public bool PlayAction(string actionName, float fadeDuration = 0.15f)
    {
        if (!actionDB.TryGetValue(actionName, out var runtimeData))
        {
            Debug.LogWarning($"[ActionDriver] Action not found: {actionName}");
            return false;
        }

        CurrentAction = runtimeData;
        ActionTime = 0f;
        ActionDuration = (float)runtimeData.SourceAction.duration;

        // 重置所有游标
        vfxCursor = sfxCursor = hitFeelCursor = hitBoxCursor = 0;

        // 停用所有正在激活的判定盒
        foreach (var evt in activeHitBoxEvents)
            OnHitBoxDeactivate?.Invoke(evt);
        activeHitBoxEvents.Clear();

        bufferedCommand = EInputCommand.None;

        OnPlayAnimation?.Invoke(runtimeData.AnimatorStateName, fadeDuration);
        return true;
    }

    /// <summary>强制播放动作，无视当前状态（用于受击/死亡等外部强制打断）</summary>
    public void ForceAction(string actionName, float fadeDuration = 0.1f)
    {
        PlayAction(actionName, fadeDuration);
    }

    /// <summary>停止当前动作</summary>
    public void Stop()
    {
        CurrentAction = null;
        ActionTime = 0f;
    }

    // ═══════════════════════════════════════
    //   输入 & 信号
    // ═══════════════════════════════════════

    /// <summary>发送指令（尝试立即匹配转移窗口，否则缓冲）</summary>
    public void SendCommand(EInputCommand command, EInputPhase phase)
    {
        if (CurrentAction == null)
        {
            Debug.LogWarning($"[ActionDriver] SendCommand({command}, {phase}) 被忽略：CurrentAction 为 null");
            return;
        }

        if (TryMatchCommand(command, phase))
            return;

        // 缓冲输入
        bufferedCommand = command;
        bufferedPhase = phase;
        bufferTimestamp = Time.time;
    }

    /// <summary>发送信号（受击 "Hit"、眩晕 "Stun" 等），返回是否有匹配的信号转移</summary>
    public bool SendSignal(string signal)
    {
        if (CurrentAction == null) return false;

        // 检查当前动作的信号转移
        foreach (var signalTransition in CurrentAction.SourceAction.signalTransitions)
        {
            if (signalTransition.Check(signal))
            {
                TransitionTo(signalTransition);
                return true;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════
    //   每帧更新
    // ═══════════════════════════════════════

    public void Update(float deltaTime)
    {
        if (CurrentAction == null) return;

        ActionTime += deltaTime;

        FireEvents();
        CheckBufferedInput();

        if (ActionTime >= ActionDuration)
            HandleActionFinished();
    }

    // ═══════════════════════════════════════
    //   事件触发
    // ═══════════════════════════════════════

    private void FireEvents()
    {
        // VFX
        while (vfxCursor < CurrentAction.VfxEvents.Count && ActionTime >= CurrentAction.VfxEvents[vfxCursor].StartTime)
            OnVfxEvent?.Invoke(CurrentAction.VfxEvents[vfxCursor++]);

        // SFX
        while (sfxCursor < CurrentAction.SfxEvents.Count && ActionTime >= CurrentAction.SfxEvents[sfxCursor].StartTime)
            OnSfxEvent?.Invoke(CurrentAction.SfxEvents[sfxCursor++]);

        // HitFeel
        while (hitFeelCursor < CurrentAction.HitFeelEvents.Count && ActionTime >= CurrentAction.HitFeelEvents[hitFeelCursor].StartTime)
            OnHitFeelEvent?.Invoke(CurrentAction.HitFeelEvents[hitFeelCursor++]);

        // HitBox 激活
        while (hitBoxCursor < CurrentAction.HitBoxEvents.Count && ActionTime >= CurrentAction.HitBoxEvents[hitBoxCursor].StartTime)
        {
            var evt = CurrentAction.HitBoxEvents[hitBoxCursor++];
            activeHitBoxEvents.Add(evt);
            OnHitBoxActivate?.Invoke(evt);
        }

        // HitBox 停用（到期的判定窗口）
        for (int i = activeHitBoxEvents.Count - 1; i >= 0; i--)
        {
            if (ActionTime >= activeHitBoxEvents[i].EndTime)
            {
                OnHitBoxDeactivate?.Invoke(activeHitBoxEvents[i]);
                activeHitBoxEvents.RemoveAt(i);
            }
        }
    }

    // ═══════════════════════════════════════
    //   转移匹配
    // ═══════════════════════════════════════

    private void CheckBufferedInput()
    {
        if (bufferedCommand == EInputCommand.None) return;
        if (Time.time - bufferTimestamp > MaxBufferAge)
        {
            bufferedCommand = EInputCommand.None;
            return;
        }

        if (TryMatchCommand(bufferedCommand, bufferedPhase))
            bufferedCommand = EInputCommand.None;
    }

    private bool TryMatchCommand(EInputCommand command, EInputPhase phase)
    {
        if (CurrentAction == null) return false;

        // 检查当前动作的指令转移窗口
        if (TryMatchWindows(CurrentAction.TransitionWindows, command, phase))
            return true;

        return false;
    }

    private bool TryMatchWindows(List<ActionTransitionWindowData> windows, EInputCommand command, EInputPhase phase)
    {
        // 对 ActionTime 取模，从而支持循环动作（如 Idle/Move）的转移窗口始终开放
        float t = (ActionDuration > 0f) ? ActionTime % ActionDuration : ActionTime;

        foreach (var window in windows)
        {
            // 窗口有效范围 = [StartTime - InputBuffer, StartTime + Duration]
            float windowStart = window.StartTime - window.InputBufferDuration;
            float windowEnd = window.StartTime + window.Duration;

            bool timeOk = t >= windowStart && t <= windowEnd;
            bool cmdOk  = window.Transition.Check(command, phase);

            if (timeOk && cmdOk)
            {
                TransitionTo(window.Transition);
                return true;
            }
        }
        return false;
    }

    // ═══════════════════════════════════════
    //   转移执行
    // ═══════════════════════════════════════

    private void TransitionTo(TransitionInfo transition)
    {
        if (string.IsNullOrEmpty(transition.targetActionName))
        {
            // 目标为空 → 回到 Idle
            ReturnToIdle(transition.fadeDuration);
            return;
        }

        PlayAction(transition.targetActionName, transition.fadeDuration);
    }

    private void HandleActionFinished()
    {
        var finishTransition = CurrentAction.SourceAction.finishTransition;

        if (finishTransition != null && !string.IsNullOrEmpty(finishTransition.targetActionName))
        {
            TransitionTo(finishTransition);
        }
        else
        {
            // 没有完成转移 → 回到 Idle
            ReturnToIdle();
        }
    }

    private void ReturnToIdle(float fadeDuration = 0.15f)
    {
        Stop();
        PlayAction(ActionNames.Idle, fadeDuration);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;

/// <summary>
/// 动作驱动器 - 基于 ActionSO 转移关系驱动动作播放，不是传统FSM
/// 所有状态转移由编辑器中配置的 CommandTransition / FinishTransition / SignalTransition 决定
/// Animator 只负责播放对应 State，不连线
/// </summary>
public class ActionPlayer
{
    private readonly Dictionary<string, ActionRuntimeData> actionDB = new();
    private readonly Dictionary<string, ActionSO> actionSODB = new();

    // ─── 当前动作状态 ───
    public ActionRuntimeData CurrentAction { get; private set; }
    public string CurrentActionName => CurrentAction?.AnimatorStateName;
    public float ActionTime { get; private set; }
    public float ActionDuration { get; private set; }
    public bool IsPlaying => CurrentAction != null;

    // ─── 输入缓冲 ───
    private EInputCommand bufferedCommand;
    private EInputPhase bufferedPhase;
    private float bufferTimestamp;
    private const float MaxBufferAge = 0.5f; // 输入缓冲最大有效时间

    // ─── 事件游标 ───
    private int vfxCursor, sfxCursor, hitFeelCursor;

    // ─── 回调 ───
    /// <summary>播放动画 (stateName, fadeDuration)</summary>
    public event Action<string, float> OnPlayAnimation;
    public event Action<ActionVfxEventData> OnVfxEvent; // VFX事件
    public event Action<ActionSfxEventData> OnSfxEvent; // SFX事件
    public event Action<ActionHitFeelEventData> OnHitFeelEvent; // 受击反馈事件
    /// <summary>动作结束回到待机</summary>
    public event Action OnReturnToLocomotion;

    // ═══════════════════════════════════════
    //   初始化
    // ═══════════════════════════════════════

    public void Init(ActionListSO actionList)
    {
        actionDB.Clear();
        actionSODB.Clear();

        foreach (var so in actionList.actions)
        {
            if (so == null) continue;

            var rd = ActionUnpacker.Unpack(so);

            // 按 StartTime 排序，便于顺序游标遍历
            rd.VfxEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            rd.SfxEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            rd.HitFeelEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            rd.TransitionWindows.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            actionDB[so.name] = rd;
            actionSODB[so.name] = so;
        }
    }

    // ═══════════════════════════════════════
    //   播放控制
    // ═══════════════════════════════════════

    /// <summary>播放指定动作</summary>
    public bool PlayAction(string actionName, float fadeDuration = 0.15f)
    {
        if (!actionDB.TryGetValue(actionName, out var data))
        {
            Debug.LogWarning($"[ActionPlayer] Action not found: {actionName}");
            return false;
        }


        CurrentAction = data;
        ActionTime = 0f;
        ActionDuration = (float)data.SourceAction.duration;

        // 重置所有游标
        vfxCursor = sfxCursor = hitFeelCursor = 0;
        bufferedCommand = EInputCommand.None;

        OnPlayAnimation?.Invoke(data.AnimatorStateName, fadeDuration);
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
        if (CurrentAction != null && TryMatchCommand(command, phase))
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

        var so = CurrentAction.SourceAction;

        // 检查当前动作的信号转移
        foreach (var st in so.signalTransitions)
        {
            if (st.Check(signal))
            {
                TransitionTo(st);
                return true;
            }
        }

        // 检查继承动作的信号转移
        if (!string.IsNullOrEmpty(so.inheritTransitionActionName) &&
            actionSODB.TryGetValue(so.inheritTransitionActionName, out var inherited))
        {
            foreach (var st in inherited.signalTransitions)
            {
                if (st.Check(signal))
                {
                    TransitionTo(st);
                    return true;
                }
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
        var act = CurrentAction;
        float t = ActionTime;

        // VFX
        while (vfxCursor < act.VfxEvents.Count && t >= act.VfxEvents[vfxCursor].StartTime)
            OnVfxEvent?.Invoke(act.VfxEvents[vfxCursor++]);

        // SFX
        while (sfxCursor < act.SfxEvents.Count && t >= act.SfxEvents[sfxCursor].StartTime)
            OnSfxEvent?.Invoke(act.SfxEvents[sfxCursor++]);

        // HitFeel
        while (hitFeelCursor < act.HitFeelEvents.Count && t >= act.HitFeelEvents[hitFeelCursor].StartTime)
            OnHitFeelEvent?.Invoke(act.HitFeelEvents[hitFeelCursor++]);
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

    private bool TryMatchCommand(EInputCommand cmd, EInputPhase phase)
    {
        if (CurrentAction == null) return false;

        // 检查当前动作的指令转移窗口
        if (TryMatchWindows(CurrentAction.TransitionWindows, cmd, phase))
            return true;

        // 检查继承动作的指令转移窗口
        var so = CurrentAction.SourceAction;
        if (!string.IsNullOrEmpty(so.inheritTransitionActionName) &&
            actionDB.TryGetValue(so.inheritTransitionActionName, out var inheritedData))
        {
            if (TryMatchWindows(inheritedData.TransitionWindows, cmd, phase))
                return true;
        }

        return false;
    }

    private bool TryMatchWindows(List<ActionTransitionWindowData> windows, EInputCommand cmd, EInputPhase phase)
    {
        float t = ActionTime;
        foreach (var tw in windows)
        {
            // 窗口有效范围 = [StartTime - InputBuffer, StartTime + Duration]
            float windowStart = tw.StartTime - tw.InputBufferDuration;
            float windowEnd = tw.StartTime + tw.Duration;

            if (t >= windowStart && t <= windowEnd && tw.Transition.Check(cmd, phase))
            {
                TransitionTo(tw.Transition);
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
            // 目标为空 → 回到待机
            Stop();
            OnReturnToLocomotion?.Invoke();
            return;
        }

        PlayAction(transition.targetActionName, transition.fadeDuration);
    }

    private void HandleActionFinished()
    {
        var ft = CurrentAction.SourceAction.finishTransition;

        if (ft != null && !string.IsNullOrEmpty(ft.targetActionName))
        {
            TransitionTo(ft);
        }
        else
        {
            // 没有完成转移 → 回到待机
            Stop();
            OnReturnToLocomotion?.Invoke();
        }
    }
}

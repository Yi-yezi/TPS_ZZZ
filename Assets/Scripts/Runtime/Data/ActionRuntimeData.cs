using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// ActionSO 的运行时快照数据。
    /// 由 ActionUnpacker 从 TimelineAsset 中提取，供运行时系统直接消费。
    /// </summary>
    public sealed class ActionRuntimeData
    {
        // 源动作资产，便于日志和回溯。
        public ActionSO SourceAction;

        // 轨道级概览信息（用于 Inspector 展示或调试）。
        public readonly List<ActionTrackSummary> TrackSummaries = new();

        // 按事件类型拆分后的运行时数据表。
        // 动画状态名（= ActionSO.name，与 Animator State 一致）
        public string AnimatorStateName;
        public readonly List<ActionVfxEventData> VfxEvents = new();
        public readonly List<ActionSfxEventData> SfxEvents = new();
        public readonly List<ActionFunctionEventData> FunctionEvents = new();
        public readonly List<ActionHitFeelEventData> HitFeelEvents = new();
        public readonly List<ActionTransitionWindowData> TransitionWindows = new();

        public int TrackCount => TrackSummaries.Count;
    }

    /// <summary>
    /// 轨道级概览信息（用于 Inspector 展示或调试）。
    /// 不再包含动画相关 — 动画由 Animator State 驱动。
    /// </summary>
    [Serializable]
    public struct ActionTrackSummary
    {
        public string TrackName;
        public string TrackType;
        public int ClipCount;
        public float StartTime;
        public float EndTime;
    }

    /// <summary>
    /// 特效事件数据（来自 VFXTrack）。
    /// </summary>
    [Serializable]
    public struct ActionVfxEventData
    {
        public string TrackName;
        public float StartTime;
        public float Duration;
        public GameObject VfxPrefab;
        public string ParentPath;
        public bool AttachToParent;
        public Vector3 PositionOffset;
        public Vector3 RotationOffset;
    }

    /// <summary>
    /// 音效事件数据（来自 SFXTrack）。
    /// </summary>
    [Serializable]
    public struct ActionSfxEventData
    {
        public string TrackName;
        public float StartTime;
        public float Duration;
        public AudioClip[] AudioClips;
        public float Volume;
        public float Pitch;
        public float PitchRandomRange;
    }

    /// <summary>
    /// 函数事件数据（来自 FunctionEventTrack）。
    /// </summary>
    [Serializable]
    public struct ActionFunctionEventData
    {
        public string TrackName;
        public float StartTime;
        public float Duration;
        public string FunctionName;
        public string StringParam;
        public int IntParam;
        public float FloatParam;
    }

    /// <summary>
    /// 打击感事件数据（来自 HitFeelTrack）。
    /// </summary>
    [Serializable]
    public struct ActionHitFeelEventData
    {
        public string TrackName;
        public float StartTime;
        public float Duration;
        public float PauseFrameDuration;
        public float ShakeForce;
        public float ShakeDuration;
    }

    /// <summary>
    /// 指令转移时间窗口（来自 CommandTransitionTrack）。
    /// </summary>
    [Serializable]
    public struct ActionTransitionWindowData
    {
        public string TrackName;
        public float StartTime;
        public float Duration;
        public float InputBufferDuration;
        public CommandTransitionInfo Transition;
    }

    /// <summary>
    /// 将 ActionSO 的 Timeline 数据展开为运行时结构。
    /// </summary>
    public static class ActionUnpacker
    {
        /// <summary>
        /// 解析动作中的所有轨道和 Clip，产出可直接消费的运行时快照。
        /// </summary>
        public static ActionRuntimeData Unpack(ActionSO action)
        {
            var data = new ActionRuntimeData
            {
                SourceAction = action,
                AnimatorStateName = action.name,
            };

            if (action == null)
                return data;

            foreach (var track in action.GetOutputTracks())
            {
                if (track == null)
                    continue;

                var clips = track.GetClips().ToList();
                float startTime = clips.Count > 0 ? (float)clips.Min(static clip => clip.start) : 0f;
                float endTime = clips.Count > 0 ? (float)clips.Max(static clip => clip.end) : 0f;

                data.TrackSummaries.Add(new ActionTrackSummary
                {
                    TrackName = track.name,
                    TrackType = track.GetType().Name,
                    ClipCount = clips.Count,
                    StartTime = startTime,
                    EndTime = endTime,
                });

                foreach (var clip in clips)
                {
                    FillClipData(data, track, clip);
                }
            }

            return data;
        }

        /// <summary>
        /// 根据轨道和 Clip 类型分发到对应的数据表。
        /// </summary>
        private static void FillClipData(ActionRuntimeData data, TrackAsset track, TimelineClip clip)
        {
            if (clip.asset is CommandTransitionClip transitionClip)
            {
                // 将时间信息回填到指令转移 Clip，方便外部直接读取。
                transitionClip.start = (float)clip.start;
                transitionClip.length = (float)clip.duration;
            }

            if (track is AnimationTrack)
            {
                // 动画由 Animator State 驱动，跳过 AnimationTrack
                return;
            }

            switch (clip.asset)
            {
                case VFXClip vfxClip:
                    data.VfxEvents.Add(new ActionVfxEventData
                    {
                        TrackName = track.name,
                        StartTime = (float)clip.start,
                        Duration = (float)clip.duration,
                        VfxPrefab = vfxClip.vfxPrefab,
                        ParentPath = vfxClip.parentPath,
                        AttachToParent = vfxClip.attachToParent,
                        PositionOffset = vfxClip.positionOffset,
                        RotationOffset = vfxClip.rotationOffset,
                    });
                    break;

                case SFXClip sfxClip:
                    data.SfxEvents.Add(new ActionSfxEventData
                    {
                        TrackName = track.name,
                        StartTime = (float)clip.start,
                        Duration = (float)clip.duration,
                        AudioClips = sfxClip.audioClips,
                        Volume = sfxClip.volume,
                        Pitch = sfxClip.pitch,
                        PitchRandomRange = sfxClip.pitchRandomRange,
                    });
                    break;

                case FunctionEventClip functionClip:
                    data.FunctionEvents.Add(new ActionFunctionEventData
                    {
                        TrackName = track.name,
                        StartTime = (float)clip.start,
                        Duration = (float)clip.duration,
                        FunctionName = functionClip.functionName,
                        StringParam = functionClip.stringParam,
                        IntParam = functionClip.intParam,
                        FloatParam = functionClip.floatParam,
                    });
                    break;

                case HitFeelClip hitFeelClip:
                    data.HitFeelEvents.Add(new ActionHitFeelEventData
                    {
                        TrackName = track.name,
                        StartTime = (float)clip.start,
                        Duration = (float)clip.duration,
                        PauseFrameDuration = hitFeelClip.pauseFrameDuration,
                        ShakeForce = hitFeelClip.shakeForce,
                        ShakeDuration = hitFeelClip.shakeDuration,
                    });
                    break;

                case CommandTransitionClip ctClip:
                    data.TransitionWindows.Add(new ActionTransitionWindowData
                    {
                        TrackName = track.name,
                        StartTime = (float)clip.start,
                        Duration = (float)clip.duration,
                        InputBufferDuration = ctClip.inputBufferDuration,
                        Transition = ctClip.commandTransition,
                    });
                    break;
            }
        }
    }
}
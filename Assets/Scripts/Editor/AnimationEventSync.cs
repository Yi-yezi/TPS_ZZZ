using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 动画事件自动同步 - 监控 AnimationTrack 中的 clip 变化，
    /// 当检测到 AnimationClip 含有事件时自动清除
    /// </summary>
    [InitializeOnLoad]
    public static class AnimationEventSync
    {
        // 缓存上一次检测到的 clip 数量，用于判断是否有新 clip 添加
        private static readonly Dictionary<int, int> _trackedClipCounts = new();
        private static double _lastCheckTime;
        private const double CHECK_INTERVAL = 1.0;

        static AnimationEventSync()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastCheckTime < CHECK_INTERVAL)
                return;
            _lastCheckTime = EditorApplication.timeSinceStartup;

            var action = GetCurrentTimelineAction();
            if (action == null) return;

            if (HasAnimationClipChanged(action))
            {
                SyncEvents(action);
            }
        }

        private static ActionSO GetCurrentTimelineAction()
        {
            // 优先从 TimelineEditor 获取当前正在编辑的资产
            var inspectedAsset = TimelineEditor.inspectedAsset;
            if (inspectedAsset is ActionSO actionFromTimeline)
                return actionFromTimeline;

            // 回退：从 Selection 获取
            var activeGo = Selection.activeGameObject;
            if (activeGo == null) return null;

            var director = activeGo.GetComponent<PlayableDirector>();
            if (director == null || director.playableAsset == null) return null;

            return director.playableAsset as ActionSO;
        }

        private static bool HasAnimationClipChanged(ActionSO action)
        {
            int instanceId = action.GetInstanceID();
            int currentCount = 0;

            foreach (var track in action.GetOutputTracks())
            {
                if (track is AnimationTrack)
                {
                    foreach (var clip in track.GetClips())
                    {
                        if (clip.animationClip != null)
                            currentCount++;
                    }
                }
            }

            if (!_trackedClipCounts.TryGetValue(instanceId, out int lastCount))
            {
                _trackedClipCounts[instanceId] = currentCount;
                return false;
            }

            if (currentCount != lastCount)
            {
                _trackedClipCounts[instanceId] = currentCount;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清除 AnimationTrack 中所有 clip 自带的 AnimationEvent，
        /// 避免 "has no receiver" 警告。
        /// </summary>
        public static void SyncEvents(ActionSO action)
        {
            if (action == null) return;

            var animTracks = action.GetOutputTracks().OfType<AnimationTrack>().ToList();
            if (animTracks.Count == 0) return;

            int strippedCount = 0;
            foreach (var animTrack in animTracks)
            {
                foreach (var clip in animTrack.GetClips())
                {
                    var animClip = clip.animationClip;
                    if (animClip == null) continue;

                    var events = AnimationUtility.GetAnimationEvents(animClip);
                    if (events != null && events.Length > 0)
                    {
                        AnimationUtility.SetAnimationEvents(animClip, System.Array.Empty<AnimationEvent>());
                        strippedCount += events.Length;
                    }
                }
            }

            if (strippedCount > 0)
            {
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AnimationEventSync] 已清除 {strippedCount} 个 AnimationClip 自带事件");
            }
        }
    }
}

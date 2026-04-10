using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 指令转移 Clip - 在时间轴上定义一个时间窗口，窗口内允许指定的指令转移生效。
    /// </summary>
    public class CommandTransitionClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>
        /// 运行时起始时间（由 ActionUnpacker 从 TimelineClip 填充）
        /// </summary>
        [NonSerialized] public float start;

        /// <summary>
        /// 运行时持续时长（由 ActionUnpacker 从 TimelineClip 填充）
        /// </summary>
        [NonSerialized] public float length;

        [Tooltip("输入缓冲时长")]
        public float inputBufferDuration = 0.1f;

        public CommandTransitionInfo commandTransition;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<CommandTransitionPlayable>.Create(graph);
        }

        /// <summary>
        /// 根据指令和目标动作生成显示名称
        /// </summary>
        public string GetDisplayName()
        {
            if (commandTransition == null)
                return "指令转移";

            string cmd = commandTransition.command.ToString();
            string target = string.IsNullOrEmpty(commandTransition.targetActionName)
                ? "?"
                : commandTransition.targetActionName;
            return $"{cmd} → {target}";
        }
    }

    /// <summary>
    /// 空的 PlayableBehaviour，让 Timeline 能正常处理 Clip
    /// </summary>
    public class CommandTransitionPlayable : PlayableBehaviour
    {
    }
}

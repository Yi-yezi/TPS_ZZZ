using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 动作SO - 继承 TimelineAsset，可直接在 Unity Timeline 编辑器中编辑事件轨道
    /// 同时在 Inspector 中配置转移关系、战斗参数等
    /// </summary>
    [CreateAssetMenu(fileName = "Action", menuName = "ActionSystem/Action")]
    public class ActionSO : TimelineAsset
    {
        [Header("战斗参数")]
        public float damage;
        public float cooldown;
        public float attackDistance;
        public float attackOffset;
        public string[] hitboxNames;
        public string[] parryNames;

        [Header("完成转移 - 动作播放完毕后自动转移")]
        public TransitionInfo finishTransition = new();

        [Header("继承转移 - 从另一个动作继承转移规则")]
        public string inheritTransitionActionName;

        [Header("信号转移 - 全局生效的信号转移")]
        public List<SignalTransitionInfo> signalTransitions = new();

        /// <summary>
        /// 获取所有指令转移轨道
        /// </summary>
        public IEnumerable<CommandTransitionTrack> GetCommandTransitionTracks()
        {
            return GetOutputTracks().OfType<CommandTransitionTrack>();
        }

        /// <summary>
        /// 获取所有指令转移 Clip 信息（每条轨道取第一个 Clip）
        /// </summary>
        public IEnumerable<(TimelineClip clip, CommandTransitionClip asset, CommandTransitionTrack track)> GetCommandTransitionClips()
        {
            foreach (var track in GetCommandTransitionTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is CommandTransitionClip ct)
                        yield return (clip, ct, track);
                }
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 打击感 Clip - 在命中时触发顿帧/震屏
    /// </summary>
    public class HitFeelClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("顿帧时长")]
        public float pauseFrameDuration = 0.05f;

        [Tooltip("震动强度")]
        public float shakeForce = 0.3f;

        [Tooltip("震动时长")]
        public float shakeDuration = 0.1f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<HitFeelPlayable>.Create(graph);
        }
    }

    public class HitFeelPlayable : PlayableBehaviour { }
}

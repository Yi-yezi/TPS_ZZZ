using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 攻击判定窗口 Clip。
    /// Clip 时间跨度 = 判定盒激活区间：进入时调用 HitBox.Activate，退出时调用 HitBox.Deactivate。
    /// hitBoxName 与预挂在武器/骨骼上的 HitBox.hitBoxName 对应。
    /// </summary>
    public class HitBoxClip : PlayableAsset, ITimelineClipAsset
    {
        [UnityEngine.Tooltip("对应预挂在武器/骨骼上的 HitBox.hitBoxName")]
        public string hitBoxName;

        [UnityEngine.Tooltip("本次判定造成的伤害值")]
        public float damage;

        public ClipCaps clipCaps => ClipCaps.None;

        public override UnityEngine.Playables.Playable CreatePlayable(PlayableGraph graph, UnityEngine.GameObject owner)
            => UnityEngine.Playables.Playable.Null;
    }
}

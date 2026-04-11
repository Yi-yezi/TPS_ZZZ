using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 攻击判定轨道：放置 HitBoxClip，Clip 时长即为判定盒激活窗口。
    /// 每条轨道对应一个判定盒，需要同时激活多个判定盒时添加多条轨道。
    /// </summary>
    [TrackClipType(typeof(HitBoxClip))]
    [TrackColor(1f, 0.55f, 0f)]
    [DisplayName("SkillSystem/攻击判定轨道")]
    public class HitBoxTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.2;
            clip.displayName = "HitBox";
        }
    }
}

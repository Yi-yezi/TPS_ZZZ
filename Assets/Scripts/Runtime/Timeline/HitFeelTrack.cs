using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 打击感轨道：用于放置 HitFeelClip。
    /// </summary>
    [TrackClipType(typeof(HitFeelClip))]
    [TrackColor(1f, 0.3f, 0.6f)]
    [DisplayName("SkillSystem/打击感轨道")]
    public class HitFeelTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.1;
            clip.displayName = "HitFeel";
        }
    }
}

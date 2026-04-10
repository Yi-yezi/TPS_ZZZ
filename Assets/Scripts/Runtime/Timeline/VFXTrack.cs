using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 特效轨道：用于放置 VFXClip。
    /// </summary>
    [TrackClipType(typeof(VFXClip))]
    [TrackColor(0.3f, 0.8f, 0.4f)]
    [DisplayName("SkillSystem/特效轨道")]
    public class VFXTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.5;
            clip.displayName = "VFX";
        }
    }
}

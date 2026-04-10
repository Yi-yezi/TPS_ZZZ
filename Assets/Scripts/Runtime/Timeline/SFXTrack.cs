using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 音效轨道：用于放置 SFXClip。
    /// </summary>
    [TrackClipType(typeof(SFXClip))]
    [TrackColor(0.9f, 0.6f, 0.2f)]
    [DisplayName("SkillSystem/音效轨道")]
    public class SFXTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.3;
            clip.displayName = "SFX";
        }
    }
}

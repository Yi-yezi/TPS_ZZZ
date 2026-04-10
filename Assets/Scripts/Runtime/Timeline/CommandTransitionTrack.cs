using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 指令转移轨道 - 专门放置 CommandTransitionClip
    /// </summary>
    [TrackClipType(typeof(CommandTransitionClip))]
    [DisplayName("SkillSystem/指令转移轨道")]
    public class CommandTransitionTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.5;
            if (clip.asset is CommandTransitionClip ct)
                clip.displayName = ct.GetDisplayName();
            else
                clip.displayName = "指令转移";
        }
    }
}

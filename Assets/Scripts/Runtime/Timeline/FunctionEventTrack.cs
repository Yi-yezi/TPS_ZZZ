using System.ComponentModel;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 函数事件轨道：用于放置 FunctionEventClip。
    /// </summary>
    [TrackClipType(typeof(FunctionEventClip))]
    [TrackColor(0.8f, 0.3f, 0.3f)]
    [DisplayName("SkillSystem/函数事件轨道")]
    public class FunctionEventTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            base.OnCreateClip(clip);
            clip.duration = 0.1;
            clip.displayName = "Event";
        }
    }
}

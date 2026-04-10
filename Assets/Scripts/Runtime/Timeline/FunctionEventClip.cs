using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// 函数事件 Clip - 在时间段内触发自定义函数（Begin/End）
    /// </summary>
    public class FunctionEventClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("函数名称（运行时回调）")]
        public string functionName;

        [Tooltip("字符串参数")]
        public string stringParam;

        [Tooltip("整数参数")]
        public int intParam;

        [Tooltip("浮点参数")]
        public float floatParam;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<FunctionEventPlayable>.Create(graph);
        }
    }

    public class FunctionEventPlayable : PlayableBehaviour { }
}

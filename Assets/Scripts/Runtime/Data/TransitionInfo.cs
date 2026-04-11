using System;

namespace SkillSystem
{
    /// <summary>
    /// 基础转移信息：目标动作 + 过渡时长
    /// </summary>
    [Serializable]
    public class TransitionInfo
    {
        public string targetActionName;
        public float fadeDuration = 0.15f;
    }

    /// <summary>
    /// 指令转移：按下特定输入指令时触发转移
    /// </summary>
    [Serializable]
    public class CommandTransitionInfo : TransitionInfo
    {
        public EInputCommand command;
        public EInputPhase phase;

        public bool Check(EInputCommand cmd, EInputPhase ph)
        {
            return command == cmd && phase == ph;
        }
    }

    /// <summary>
    /// 指令转移条目：指令转移 + 时间窗口参数
    /// </summary>
    [Serializable]
    public class CommandTransitionEntry
    {
        public CommandTransitionInfo transition = new();
        public float startTime;
        public float duration = 0.5f;
        public float inputBufferDuration = 0.1f;
    }

    /// <summary>
    /// 信号转移：接收到特定信号时触发转移
    /// </summary>
    [Serializable]
    public class SignalTransitionInfo : TransitionInfo
    {
        public string signalName;

        public bool Check(string signal)
        {
            return signalName == signal;
        }
    }
}

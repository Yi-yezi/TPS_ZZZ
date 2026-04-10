using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    /// <summary>
    /// SFX 事件 Clip - 在指定时间点播放音效，支持音频组随机选择
    /// </summary>
    public class SFXClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("音效组（播放时随机选择其中一个）")]
        public AudioClip[] audioClips;

        [Tooltip("音量")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("音调")]
        [Range(0.1f, 3f)]
        public float pitch = 1f;

        [Tooltip("音调随机偏移范围")]
        [Range(0f, 0.5f)]
        public float pitchRandomRange = 0f;

        public ClipCaps clipCaps => ClipCaps.None;

        /// <summary>
        /// 从音频组中随机获取一个 AudioClip
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (audioClips == null || audioClips.Length == 0) return null;
            return audioClips[UnityEngine.Random.Range(0, audioClips.Length)];
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SFXPlayable>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.sfxClip = this;
            behaviour.owner = owner;
            return playable;
        }
    }

    public class SFXPlayable : PlayableBehaviour
    {
        private const double SeekThreshold = 0.05d;

        public SFXClip sfxClip;
        public GameObject owner;

        private GameObject _audioGo;
        private AudioSource _audioSource;
        private bool _played;
        private double _lastTime = -1;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (sfxClip == null) return;

            double localTime = playable.GetTime();
            double duration = playable.GetDuration();

            if (localTime > duration) // 超出 Clip 时长，确保音效停止并销毁预览对象

            {
                DestroyAudioSource();
                return;
            }

            if (!ShouldProcessAudioPreview(info)) // 根据播放状态和编辑器/运行时环境决定是否处理音频预览
                return;

            EnsureAudioSource(); // 确保 AudioSource 存在（编辑器预览时可能不存在）

            if (_audioSource == null) return;

            bool justEntered = !_played;
            bool seekedBack = _lastTime > localTime + SeekThreshold; // 判断是否从后面跳转回当前 Clip（如时间轴拖动或 Loop）



            if (justEntered || seekedBack)
            {
                _audioSource.Stop();

                var clip = sfxClip.GetRandomClip();
                if (clip != null)
                {
                    _audioSource.clip = clip;
                    _audioSource.volume = sfxClip.volume;
                    _audioSource.pitch = sfxClip.pitch + UnityEngine.Random.Range(-sfxClip.pitchRandomRange, sfxClip.pitchRandomRange);
                    _audioSource.time = Mathf.Clamp((float)localTime, 0f, clip.length - 0.01f);
                    _audioSource.Play();
                }
                _played = true;
            }

            _lastTime = localTime;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (info.effectivePlayState == PlayState.Paused)
                DestroyAudioSource();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            DestroyAudioSource();
        }

        private static bool ShouldProcessAudioPreview(FrameData info)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return info.evaluationType == FrameData.EvaluationType.Playback
                    && info.deltaTime > 0;
#endif
            return true;
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null) return;

            _audioGo = new GameObject("[SFX Preview]");
            _audioGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

            // 放在 owner 位置（保证 3D 音效空间位置正确）
            if (owner != null)
                _audioGo.transform.position = owner.transform.position;

            _audioSource = _audioGo.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D 音效，确保预览时始终能听到
        }

        private void DestroyAudioSource()
        {
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();

            if (_audioGo != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(_audioGo);
#else
                UnityEngine.Object.Destroy(_audioGo);
#endif
            }

            _audioGo = null;
            _audioSource = null;
            _played = false;
            _lastTime = -1;
        }
    }
}

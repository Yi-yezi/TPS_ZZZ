using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem
{
    // ============================================================
    // VFX Clip - 在时间轴上控制特效的播放
    // ============================================================

    /// <summary>
    /// VFX 事件 Clip - 在指定时间段内播放特效
    /// </summary>
    public class VFXClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("特效预制体")]
        public GameObject vfxPrefab;

        [Tooltip("参考点路径（骨骼/子物体，留空则以角色根位置为基准）")]
        public string parentPath;

        [Tooltip("是否挂载到参考点（勾选则跟随移动，否则仅以其位置生成后固定）")]
        public bool attachToParent;

        [Tooltip("位置偏移")]
        public Vector3 positionOffset;

        [Tooltip("旋转偏移")]
        public Vector3 rotationOffset;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<VFXPlayable>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.vfxPrefab = vfxPrefab;
            behaviour.parentPath = parentPath;
            behaviour.attachToParent = attachToParent;
            behaviour.positionOffset = positionOffset;
            behaviour.rotationOffset = rotationOffset;
            behaviour.owner = owner;
            behaviour.clipAsset = this;
            return playable;
        }
    }

    public class VFXPlayable : PlayableBehaviour
    {
        public GameObject vfxPrefab;
        public string parentPath;
        public bool attachToParent;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public GameObject owner;
        public VFXClip clipAsset;

        private GameObject _instance; // 当前活跃的特效实例（如果有）
        private ParticleSystem[] _particleSystems;

        /// <summary>
        /// 活跃的 VFXClip → 预览实例映射，供编辑器 Scene 工具使用
        /// </summary>
        public static readonly Dictionary<VFXClip, GameObject> ActiveInstances = new();

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (vfxPrefab == null) return;

            // 计算当前 clip 内的本地时间
            double localTime = playable.GetTime();
            double duration = playable.GetDuration();

            // 时间在有效范围内，确保实例存在
            if (localTime >= 0 && localTime <= duration)
            {
                EnsureInstance();
                UpdateTransformFromClip();
                SimulateParticles((float)localTime);
            }
            else
            {
                DestroyInstance();
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 当播放头离开 clip 区段时销毁
            // 注意：info.effectivePlayState 检查，防止 graph 停止时误判
            if (info.effectivePlayState == PlayState.Paused)
                DestroyInstance();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            DestroyInstance();
        }

        private void EnsureInstance()
        {
            if (_instance != null) return;

            _instance = UnityEngine.Object.Instantiate(vfxPrefab);
            _instance.name = $"[VFX Preview] {vfxPrefab.name}";
            _instance.hideFlags = HideFlags.DontSave;

            ApplyTransform();

            // 缓存粒子系统，停止自动播放
            _particleSystems = _instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in _particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
            }

            if (clipAsset != null)
                ActiveInstances[clipAsset] = _instance;
        }

        /// <summary>
        /// 从 VFXClip 资产同步最新的偏移值到实例
        /// 仅在 attachToParent 时每帧跟随；否则只在配置变化时更新
        /// </summary>
        private void UpdateTransformFromClip()
        {
            if (_instance == null || clipAsset == null) return;

            parentPath = clipAsset.parentPath;
            attachToParent = clipAsset.attachToParent;
            positionOffset = clipAsset.positionOffset;
            rotationOffset = clipAsset.rotationOffset;

            ApplyTransform();
        }

        /// <summary>
        /// 统一的 Transform 定位逻辑：
        /// attachToParent=true  → 相对参考点（编辑器用世界坐标模拟，运行时 SetParent）
        /// attachToParent=false → positionOffset/rotationOffset 即世界坐标
        /// </summary>
        private void ApplyTransform()
        {
            if (!attachToParent)
            {
                if (_instance.transform.parent != null)
                    _instance.transform.SetParent(null, false);
                _instance.transform.position = positionOffset;
                _instance.transform.rotation = Quaternion.Euler(rotationOffset);
                return;
            }

            var refTransform = FindParentTransform();
            if (refTransform == null)
            {
                _instance.transform.position = positionOffset;
                _instance.transform.rotation = Quaternion.Euler(rotationOffset);
                return;
            }

            if (_instance.transform.parent != refTransform)
                _instance.transform.SetParent(refTransform, false);
            _instance.transform.localPosition = positionOffset;
            _instance.transform.localRotation = Quaternion.Euler(rotationOffset);
        }

        /// <summary>
        /// 将所有粒子系统模拟到指定的本地时间，实现拖动时间轴实时预览
        /// </summary>
        private void SimulateParticles(float localTime)
        {
            if (_particleSystems == null) return;

            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;
                // 先清除再从头模拟到目标时间，保证拖动回退也正确
                ps.Simulate(localTime, true, true);
            }
        }

        private void DestroyInstance()
        {
            if (_instance == null) return;

            if (clipAsset != null)
                ActiveInstances.Remove(clipAsset);

            _particleSystems = null;
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(_instance);
#else
            UnityEngine.Object.Destroy(_instance);
#endif
            _instance = null;
        }

        /// <summary>
        /// 根据 parentPath 查找挂载点 Transform。
        /// 留空返回 null（世界空间），"" 返回 null，"." 返回 owner 自身。
        /// </summary>
        private Transform FindParentTransform()
        {
            if (string.IsNullOrEmpty(parentPath) || owner == null)
                return null;

            if (parentPath == ".")
                return owner.transform;

            var found = owner.transform.Find(parentPath);
            if (found != null) return found;

            // 深度搜索（支持只填骨骼名不填完整路径）
            return FindChildRecursive(owner.transform, parentPath);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}

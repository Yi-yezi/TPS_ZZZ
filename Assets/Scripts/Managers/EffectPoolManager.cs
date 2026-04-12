using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特效 & 音效对象池管理器（单例）。
/// VFX: 按 prefab 分池，取出时激活、到期自动回收。
/// SFX: 复用 AudioSource 对象，播放完毕自动回收。
/// </summary>
public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance { get; private set; }

    [Tooltip("每个 VFX prefab 的初始池容量")]
    public int DefaultVfxPoolSize = 4;

    [Tooltip("SFX AudioSource 初始池容量")]
    public int DefaultSfxPoolSize = 8;

    // ── VFX 池 ──────────────────────────────────────────
    private readonly Dictionary<int, Queue<GameObject>> _vfxPools = new();

    // ── SFX 池 ──────────────────────────────────────────
    private readonly Queue<AudioSource> _sfxPool = new();
    private Transform _sfxRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _sfxRoot = new GameObject("SFXPool").transform;
        _sfxRoot.SetParent(transform);
        for (int i = 0; i < DefaultSfxPoolSize; i++)
            _sfxPool.Enqueue(CreateSfxSource());
    }

    // ══════════════════════════════════════════════════════
    //  VFX
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 从池中获取一个 VFX 实例，duration 秒后自动回收。
    /// </summary>
    public GameObject SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation,
                               float duration, Transform parent = null)
    {
        if (prefab == null) return null;

        int id = prefab.GetInstanceID();
        var obj = GetFromVfxPool(id, prefab);

        var t = obj.transform;
        if (parent != null)
        {
            t.SetParent(parent);
            t.localPosition = position - parent.position;
            t.localRotation = rotation;
        }
        else
        {
            t.SetParent(null);
            t.SetPositionAndRotation(position, rotation);
        }

        obj.SetActive(true);

        float life = duration > 0 ? duration : 3f;
        StartCoroutine(RecycleVfxAfter(id, obj, life));
        return obj;
    }

    private GameObject GetFromVfxPool(int id, GameObject prefab)
    {
        if (_vfxPools.TryGetValue(id, out var queue))
        {
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                if (obj != null) return obj;          // 跳过被意外销毁的
            }
        }
        // 池空 → 新建
        var inst = Instantiate(prefab);
        inst.SetActive(false);
        return inst;
    }

    private System.Collections.IEnumerator RecycleVfxAfter(int id, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        RecycleVfx(id, obj);
    }

    private void RecycleVfx(int id, GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (!_vfxPools.TryGetValue(id, out var queue))
        {
            queue = new Queue<GameObject>();
            _vfxPools[id] = queue;
        }
        queue.Enqueue(obj);
    }

    // ══════════════════════════════════════════════════════
    //  SFX
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 在指定位置播放一个音效，播放完毕自动回收 AudioSource。
    /// </summary>
    public void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f,
                        float pitch = 1f, float pitchRandomRange = 0f)
    {
        if (clip == null) return;

        var src = GetSfxSource();
        src.transform.position = position;
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch + Random.Range(-pitchRandomRange, pitchRandomRange);
        src.Play();

        StartCoroutine(RecycleSfxAfter(src, clip.length / Mathf.Max(Mathf.Abs(src.pitch), 0.01f)));
    }

    private AudioSource GetSfxSource()
    {
        while (_sfxPool.Count > 0)
        {
            var src = _sfxPool.Dequeue();
            if (src != null) return src;
        }
        return CreateSfxSource();
    }

    private AudioSource CreateSfxSource()
    {
        var go = new GameObject("PooledSFX");
        go.transform.SetParent(_sfxRoot);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;   // 3D 音效
        return src;
    }

    private System.Collections.IEnumerator RecycleSfxAfter(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay + 0.05f);
        RecycleSfx(src);
    }

    private void RecycleSfx(AudioSource src)
    {
        if (src == null) return;
        src.Stop();
        src.clip = null;
        src.transform.SetParent(_sfxRoot);
        _sfxPool.Enqueue(src);
    }
}

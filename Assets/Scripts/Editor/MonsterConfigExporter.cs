using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkillSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 编辑器工具：导出怪物配置 JSON，供服务器读取。
/// 从怪物预制体上的 MonsterBehaviour + CombatEntity + ActionListSO 中提取：
///   - AI 参数（检测/攻击距离、冷却等）
///   - HP
///   - 每个攻击动作的 HitBox 数据（伤害、时机、范围、总时长）
/// </summary>
public static class MonsterConfigExporter
{
    [System.Serializable]
    private class ExportedHitBox
    {
        public float damage;
        public float attackDistance;
        public float attackAngle;
        public float startTime;
        public float endTime;
    }

    [System.Serializable]
    private class ExportedAction
    {
        public string actionName;
        public float duration;
        public float rootMotionSpeed;  // Root Motion 水平速度（米/秒）
        public List<ExportedHitBox> hitBoxes = new();
    }

    [System.Serializable]
    private class ExportedMonsterConfig
    {
        public string monsterType;
        public float maxHp;
        public float detectRadius;
        public float attackRadius;
        public float attackCooldown;
        public float rotationSpeed;
        public float heavyHitDamageThreshold;
        public List<ExportedAction> actions = new();
    }

    [MenuItem("Tools/导出怪物配置 JSON")]
    public static void Export()
    {
        // 查找场景中或 Prefab 中所有 MonsterBehaviour
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int count = 0;

        // 确保输出目录存在
        string outputDir = Path.Combine(Application.dataPath, "..", "Server", "Config");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var mb = prefab.GetComponent<MonsterBehaviour>();
            if (mb == null) continue;

            var config = ExtractConfig(mb, prefab.name);
            string json = JsonUtility.ToJson(config, true);
            string outPath = Path.Combine(outputDir, $"{prefab.name}.json");
            File.WriteAllText(outPath, json);
            count++;
            Debug.Log($"[MonsterConfigExporter] Exported: {outPath}");
        }

        if (count == 0)
            Debug.LogWarning("[MonsterConfigExporter] No prefabs with MonsterBehaviour found in Assets/Prefabs.");
        else
            Debug.Log($"[MonsterConfigExporter] Done. Exported {count} monster config(s) to Server/Config/");
    }

    private static ExportedMonsterConfig ExtractConfig(MonsterBehaviour mb, string prefabName)
    {
        var combat = mb.GetComponent<CombatEntity>();

        var config = new ExportedMonsterConfig
        {
            monsterType = prefabName,
            maxHp = combat != null ? combat.maxHp : 100f,
            detectRadius = mb.detectRadius,
            attackRadius = mb.attackRadius,
            attackCooldown = mb.attackCooldown,
            rotationSpeed = mb.rotationSpeed,
            heavyHitDamageThreshold = mb.heavyHitDamageThreshold,
        };

        if (mb.actionList == null) return config;

        foreach (var actionSO in mb.actionList.actions)
        {
            if (actionSO == null) continue;

            var runtimeData = ActionUnpacker.Unpack(actionSO);
            var exportedAction = new ExportedAction
            {
                actionName = actionSO.name,
                duration = (float)actionSO.duration,
                rootMotionSpeed = ExtractRootMotionSpeed(actionSO),
            };

            foreach (var hb in runtimeData.HitBoxEvents)
            {
                exportedAction.hitBoxes.Add(new ExportedHitBox
                {
                    damage = hb.Damage,
                    attackDistance = hb.AttackDistance,
                    attackAngle = hb.AttackAngle,
                    startTime = hb.StartTime,
                    endTime = hb.EndTime,
                });
            }

            config.actions.Add(exportedAction);
        }

        return config;
    }

    /// <summary>从 ActionSO（TimelineAsset）的 AnimationTrack 提取 Root Motion 水平速度。</summary>
    private static float ExtractRootMotionSpeed(ActionSO actionSO)
    {
        var animTrack = actionSO.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault();
        if (animTrack == null) return 0f;

        var firstClip = animTrack.GetClips().FirstOrDefault();
        if (firstClip?.animationClip == null) return 0f;

        var avg = firstClip.animationClip.averageSpeed;
        return new Vector3(avg.x, 0, avg.z).magnitude;
    }
}

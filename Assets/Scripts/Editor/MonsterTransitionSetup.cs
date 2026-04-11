using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using SkillSystem;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 怪兽动作转移关系一键配置工具
    /// 菜单：ActionSystem/配置怪兽转移关系
    ///
    /// 怪兽由行为树（MonsterBehaviour）直接调用 PlayAction / ForceAction，
    /// 不依赖玩家输入指令，因此所有动作的 commandTransitions 均为空。
    /// 只需配置 finishTransition（动画播完后的自动衔接）。
    /// </summary>
    public static class MonsterTransitionSetup
    {
        private const string AssetFolder = "Assets/Actions/怪兽";

        // ── 动作名常量 ──────────────────────────────────────────
        private const string Idle          = "Idle";
        private const string Walk          = "Walk";
        private const string WalkStart     = "WalkStart";
        private const string Run           = "Run";
        private const string RunStart      = "RunStart";
        private const string RunEnd        = "RunEnd";
        private const string Attack01      = "Attack01";
        private const string Attack02      = "Attack02";
        private const string Dodge         = "Dodge";
        private const string HitStay       = "HitStay";
        private const string HitFrontLight = "HitFrontLight";
        private const string HitFrontHeavy = "HitFrontHeavy";
        private const string Dead          = "Dead";

        [MenuItem("ActionSystem/配置怪兽转移关系")]
        public static void Setup()
        {
            // ── 转移表定义 ─────────────────────────────────────
            // (actionName, finishTarget, commandTransitions[])
            // 怪兽无玩家输入，commandTransitions 全部为空，仅用 finishTransition 驱动自动衔接
            var table = new List<(string name, string finish, CommandEntry[] cmds)>
            {
                // ─ 待机 / 移动 ─────────────────────────────────
                ( Idle,      Idle,  new CommandEntry[0] ),   // 循环待机
                ( Walk,      Walk,  new CommandEntry[0] ),   // 循环行走
                ( WalkStart, Walk,  new CommandEntry[0] ),   // 起步 → 行走
                ( Run,       Run,   new CommandEntry[0] ),   // 循环奔跑
                ( RunStart,  Run,   new CommandEntry[0] ),   // 起跑 → 奔跑
                ( RunEnd,    Idle,  new CommandEntry[0] ),   // 刹车 → 待机

                // ─ 战斗 ────────────────────────────────────────
                ( Attack01,  Idle,  new CommandEntry[0] ),   // 攻击1 → 待机
                ( Attack02,  Idle,  new CommandEntry[0] ),   // 攻击2 → 待机
                ( Dodge,     Idle,  new CommandEntry[0] ),   // 闪避  → 待机

                // ─ 受击 ────────────────────────────────────────
                // 受击动作播完后回到待机；MonsterBehaviour 的协程也会处理，双保险
                ( HitStay,       Idle, new CommandEntry[0] ),
                ( HitFrontLight, Idle, new CommandEntry[0] ),
                ( HitFrontHeavy, Idle, new CommandEntry[0] ),

                // ─ 死亡（终态）────────────────────────────────
                ( Dead, "", new CommandEntry[0] ),
            };

            // ── 应用到 Asset ───────────────────────────────────
            int success = 0, missing = 0;

            foreach (var (name, finish, cmds) in table)
            {
                string path = $"{AssetFolder}/{name}.asset";
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(path);
                if (action == null)
                {
                    Debug.LogWarning($"[MonsterSetup] 找不到 asset: {path}");
                    missing++;
                    continue;
                }

                var so = new SerializedObject(action);

                // finishTransition
                so.FindProperty("finishTransition.targetActionName").stringValue = finish;

                float animDur = GetAnimDuration(action);

                // commandTransitions（怪兽均为空）
                var listProp = so.FindProperty("commandTransitions");
                listProp.ClearArray();
                listProp.arraySize = cmds.Length;
                for (int i = 0; i < cmds.Length; i++)
                {
                    float dur = cmds[i].dur > 0f ? cmds[i].dur
                              : cmds[i].dur < 0f ? Mathf.Max(0f, animDur - cmds[i].start)
                              : animDur;
                    var entry = listProp.GetArrayElementAtIndex(i);
                    var t = entry.FindPropertyRelative("transition");
                    t.FindPropertyRelative("command").enumValueIndex       = (int)cmds[i].command;
                    t.FindPropertyRelative("phase").enumValueIndex         = (int)cmds[i].phase;
                    t.FindPropertyRelative("targetActionName").stringValue = cmds[i].target;
                    t.FindPropertyRelative("fadeDuration").floatValue      = cmds[i].fade;
                    entry.FindPropertyRelative("startTime").floatValue           = cmds[i].start;
                    entry.FindPropertyRelative("duration").floatValue            = dur;
                    entry.FindPropertyRelative("inputBufferDuration").floatValue = cmds[i].buffer;
                }

                so.ApplyModifiedProperties();

                RebuildCommandTransitionTracks(action);

                EditorUtility.SetDirty(action);
                success++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MonsterSetup] 配置完成：{success} 个成功，{missing} 个未找到");
            EditorUtility.DisplayDialog("怪兽转移配置", $"完成！{success} 个动作已配置。", "OK");
        }

        // ── 辅助类型 ──────────────────────────────────────────

        private struct CommandEntry
        {
            public EInputCommand command;
            public EInputPhase   phase;
            public string        target;
            public float         fade;
            public float         start;
            public float         dur;
            public float         buffer;
        }

        private static CommandEntry Cmd(EInputCommand command, EInputPhase phase, string target,
                                        float fade, float start, float dur, float buffer)
            => new CommandEntry { command = command, phase = phase, target = target,
                                  fade = fade, start = start, dur = dur, buffer = buffer };

        private static float GetAnimDuration(ActionSO action)
        {
            float maxEnd = 0f;
            foreach (var track in action.GetOutputTracks())
            {
                if (track is not AnimationTrack) continue;
                foreach (var clip in track.GetClips())
                    maxEnd = Mathf.Max(maxEnd, (float)clip.end);
            }
            return maxEnd > 0f ? maxEnd : 1f;
        }

        private static void RebuildCommandTransitionTracks(ActionSO action)
        {
            foreach (var track in action.GetCommandTransitionTracks().ToList())
                action.DeleteTrack(track);

            if (action.commandTransitions == null) return;

            foreach (var entry in action.commandTransitions)
            {
                if (entry?.transition == null) continue;

                string displayName = $"{entry.transition.command} → {entry.transition.targetActionName ?? "?"}";
                var track = action.CreateTrack<CommandTransitionTrack>(null, displayName);
                var timelineClip = track.CreateClip<CommandTransitionClip>();

                if (timelineClip.asset is CommandTransitionClip clipAsset)
                {
                    clipAsset.inputBufferDuration = entry.inputBufferDuration;
                    clipAsset.commandTransition = entry.transition;
                    EditorUtility.SetDirty(clipAsset);
                }

                timelineClip.start = entry.startTime;
                timelineClip.duration = entry.duration > 0 ? entry.duration : 0.5;
                timelineClip.displayName = displayName;
            }
        }
    }
}

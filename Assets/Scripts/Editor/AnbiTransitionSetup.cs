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
    /// 安比动作转移关系一键配置工具
    /// 菜单：ActionSystem/配置安比转移关系
    /// </summary>
    public static class AnbiTransitionSetup
    {
        private const string AssetFolder = "Assets/Actions/安比";

        // ── 动作名常量 ──────────────────────────────────────────
        // 与 ActionSO.name / Animator State 名一致，不含 SubStateMachine 前缀
        private const string Idle         = "Idle";
        private const string WalkStart    = "WalkStart";
        private const string Move         = "Move";
        private const string RunEnd       = "RunEnd";
        private const string TurnBack     = "TurnBack";
        private const string DodgeFront   = "DodgeFront";
        private const string DodgeBack    = "DodgeBack";
        private const string AttackRush    = "AttackRush";
        private const string AttackRushEnd  = "AttackRushEnd";
        private const string SkillQ         = "SkillQ";
        private const string SkillQEnd      = "SkillQEnd";
        private const string Atk01        = "Attack_Normal_01";
        private const string Atk01End     = "Attack_Normal_01_End";
        private const string Atk02        = "Attack_Normal_02";
        private const string Atk02End     = "Attack_Normal_02_End";
        private const string Atk03        = "Attack_Normal_03";
        private const string Atk03End     = "Attack_Normal_03_End";
        private const string Atk04        = "Attack_Normal_04";
        private const string Atk04End     = "Attack_Normal_04_End";
        private const string HitFront     = "HitFrontLight";
        private const string Dead         = "Dead";

        [MenuItem("ActionSystem/配置安比转移关系")]
        public static void Setup()
        {
            // ── 转移表定义 ─────────────────────────────────────
            // (actionName, finishTarget, commandTransitions[])
            // CommandEntry: (command, phase, target, fadeDuration, startTime, duration, bufferDuration)
            var table = new List<(string name, string finish, CommandEntry[] cmds)>
            {
                // ─ Locomotion ──────────────────────────────────
                // duration = 0f 表示自动取该动作的动画 clip 时长
                ( Idle,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,      EInputPhase.Down, Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,       EInputPhase.Down, SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront,  EInputPhase.Down, DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,   EInputPhase.Down, DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,        EInputPhase.Press,WalkStart,  0.10f, 0f, 0f, 0.10f),
                  }),

                ( WalkStart,
                  Move,
                  new[] {
                      Cmd(EInputCommand.Attack,      EInputPhase.Down, AttackRush,  0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,       EInputPhase.Down, SkillQ,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront,  EInputPhase.Down, DodgeFront,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,        EInputPhase.Up,   RunEnd,      0.10f, 0f, 0f, 0.05f),
                  }),

                ( Move,
                  Move,
                  new[] {
                      Cmd(EInputCommand.Attack,      EInputPhase.Down, AttackRush,  0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,       EInputPhase.Down, SkillQ,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront,  EInputPhase.Down, DodgeFront,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,        EInputPhase.Up,   RunEnd,      0.10f, 0f, 0f, 0.05f),
                  }),

                ( RunEnd,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.10f),
                  }),

                ( TurnBack,
                  Move,
                  new[] {
                      Cmd(EInputCommand.Attack, EInputPhase.Down, AttackRush,  0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Move,   EInputPhase.Up,   RunEnd,      0.10f, 0f, 0f, 0.05f),
                  }),

                // ─ Skill ──────────────────────────────────────────
                // 不可打断，播完自动转 SkillQEnd → Idle
                ( SkillQ,    SkillQEnd, new CommandEntry[0] ),
                ( SkillQEnd, Idle, new[] {
                    Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                    Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                    Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                    Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                    Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.05f),
                }),

                // ─ Dodge ───────────────────────────────────────
                // 后半段（~0.30s 起）可不等播完直接衔接其他动作
                // startTime=0.30  duration=0.40  → 窗口 [0.20, 0.70]（含 0.10 buffer），Inspector 可微调
                ( DodgeFront, Idle, new[] {
                    Cmd(EInputCommand.DodgeFront,  EInputPhase.Down, DodgeFront, 0.10f, 0.50f, -1.0f, 0.10f),
                    Cmd(EInputCommand.DodgeBack,   EInputPhase.Down, DodgeBack,  0.10f, 0.50f, -1.0f, 0.10f),
                    Cmd(EInputCommand.Attack,      EInputPhase.Down, AttackRush, 0.10f, 0.50f, -1.0f, 0.10f),
                    Cmd(EInputCommand.Move,        EInputPhase.Press, Move,      0.10f, 0.50f, -1.0f, 0.05f),
                }),

                ( DodgeBack,  Idle, new[] {
                    Cmd(EInputCommand.DodgeFront,  EInputPhase.Down, DodgeFront, 0.10f, 0.60f, -1.0f, 0.10f),
                    Cmd(EInputCommand.DodgeBack,   EInputPhase.Down, DodgeBack,  0.10f, 0.60f, -1.0f, 0.10f),
                    Cmd(EInputCommand.Attack,      EInputPhase.Down, Atk01,      0.10f, 0.60f, -1.0f, 0.10f),
                    Cmd(EInputCommand.Move,        EInputPhase.Press, WalkStart, 0.10f, 0.60f, -1.0f, 0.05f),
                }),

                // ─ Combat 跑攻 ─────────────────────────────────
                // startTime=0 duration=0 → 全程可打断
                ( AttackRush,
                  AttackRushEnd,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0.60f, -1.0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0.60f, -1.0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0.60f, -1.0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0.60f, -1.0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0.60f, -1.0f, 0.05f),
                  }),

                ( AttackRushEnd,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0.60f, -1.0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0.60f, -1.0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0.60f, -1.0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0.60f, -1.0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0.60f, -1.0f, 0.05f),
                  }),

                // ─ Combat 连招链 ────────────────────────────────
                // 每段攻击动画的连招窗口时间（startTime / duration）
                // 请根据实际动画时长在 Inspector 中微调
                ( Atk01,
                  Atk01End,
                  new[] {
                      Cmd(EInputCommand.Skill,  EInputPhase.Down, SkillQ, 0.10f, 0.20f, 0.50f, 0.15f),
                      Cmd(EInputCommand.Attack, EInputPhase.Down, Atk02,  0.05f, 0.20f, 0.50f, 0.15f),
                  }),

                ( Atk01End,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.05f),
                  }),

                ( Atk02,
                  Atk02End,
                  new[] {
                      Cmd(EInputCommand.Skill,  EInputPhase.Down, SkillQ, 0.10f, 0.20f, 0.50f, 0.15f),
                      Cmd(EInputCommand.Attack, EInputPhase.Down, Atk03,  0.05f, 0.20f, 0.50f, 0.15f),
                  }),

                ( Atk02End,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.05f),
                  }),

                ( Atk03,
                  Atk03End,
                  new[] {
                      Cmd(EInputCommand.Skill,  EInputPhase.Down, SkillQ, 0.10f, 0.20f, 0.50f, 0.15f),
                      Cmd(EInputCommand.Attack, EInputPhase.Down, Atk04,  0.05f, 0.20f, 0.50f, 0.15f),
                  }),

                ( Atk03End,
                  Idle,
                  new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.05f),
                  }),

                ( Atk04,    Atk04End, new[] {
                      Cmd(EInputCommand.Skill, EInputPhase.Down, SkillQ, 0.10f, 0.20f, 0.50f, 0.15f),
                }),
                ( Atk04End, Idle, new[] {
                      Cmd(EInputCommand.Attack,     EInputPhase.Down,  Atk01,      0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.Skill,      EInputPhase.Down,  SkillQ,     0.10f, 0f, 0f, 0.15f),
                      Cmd(EInputCommand.DodgeFront, EInputPhase.Down,  DodgeFront, 0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.DodgeBack,  EInputPhase.Down,  DodgeBack,  0.10f, 0f, 0f, 0.10f),
                      Cmd(EInputCommand.Move,       EInputPhase.Press, Move,       0.10f, 0f, 0f, 0.05f),
                  }),

                // ─ Hit ─────────────────────────────────────────
                ( HitFront, Idle, new CommandEntry[0] ),

                // ─ Dead (终态) ─────────────────────────────────
                ( Dead, "", new CommandEntry[0] ),
            };
            // ── 信号转移表（按动作名配置）───────────────────────────────────────
            // 所有状态四向受击均可打断（前/后 × 轻/重）
            // 安比暂无背面受击动画，所有受击信号均映射到 HitFront
            // 无敌帧：DodgeFront / DodgeBack
            // 终态不配置：HitFront / Dead
            var signalTable = new System.Collections.Generic.Dictionary<string, SignalEntry[]>
            {
                [Idle]          = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [WalkStart]     = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Move]          = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [RunEnd]        = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [TurnBack]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [AttackRush]    = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [AttackRushEnd] = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk01]         = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk01End]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk02]         = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk02End]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk03]         = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk03End]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk04]         = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [Atk04End]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [SkillQ]        = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                [SkillQEnd]     = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
                // 受击中再次被击：重新进入受击动作（连续打击效果）
                [HitFront]      = new[] { Sig(HitSignals.HitLightFront, HitFront), Sig(HitSignals.HitLightBack, HitFront), Sig(HitSignals.HitHeavyFront, HitFront), Sig(HitSignals.HitHeavyBack, HitFront) },
            };
            // ── 攻击判定轨道表（start/end 为参考默认値，请在 Timeline 中微调）─────────────
            var hitBoxTable = new System.Collections.Generic.Dictionary<string, HitBoxEntry[]>
            {
                [AttackRush] = new[] { HBx(0.20f, 0.45f, 15f, 2.5f, 90f) },
                [Atk01]      = new[] { HBx(0.15f, 0.35f, 10f, 2.0f, 80f) },
                [Atk02]      = new[] { HBx(0.15f, 0.35f, 10f, 2.0f, 80f) },
                [Atk03]      = new[] { HBx(0.20f, 0.60f, 12f, 2.0f, 80f) },
                [Atk04]      = new[] { HBx(0.25f, 0.75f, 25f, 2.0f, 80f) },
                [SkillQ]     = new[] { HBx(0.40f, 0.60f, 30f, 2.5f, 90f) },
            };
            // ── 应用到 Asset ───────────────────────────────────
            int success = 0, missing = 0;

            foreach (var (name, finish, cmds) in table)
            {
                string path = $"{AssetFolder}/{name}.asset";
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(path);
                if (action == null)
                {
                    Debug.LogWarning($"[AnbiSetup] 找不到 asset: {path}");
                    missing++;
                    continue;
                }

                var so = new SerializedObject(action);

                // finishTransition
                so.FindProperty("finishTransition.targetActionName").stringValue = finish;

                // duration = 0f 时自动取动画 clip 时长
                float animDur = GetAnimDuration(action);

                // commandTransitions
                var listProp = so.FindProperty("commandTransitions");
                listProp.ClearArray();
                listProp.arraySize = cmds.Length;
                for (int i = 0; i < cmds.Length; i++)
                {
                    // dur =  0  → 整段动画时长（全程窗口，适合 startTime=0）
                    // dur = -1  → animDur - startTime（从 startTime 到结尾）
                    // dur >  0  → 直接使用指定值
                    float dur = cmds[i].dur > 0f ? cmds[i].dur
                              : cmds[i].dur < 0f ? Mathf.Max(0f, animDur - cmds[i].start)
                              : animDur;
                    var entry = listProp.GetArrayElementAtIndex(i);
                    var t = entry.FindPropertyRelative("transition");
                    t.FindPropertyRelative("command").enumValueIndex       = (int)cmds[i].command;
                    t.FindPropertyRelative("phase").enumValueIndex         = (int)cmds[i].phase;
                    t.FindPropertyRelative("targetActionName").stringValue = cmds[i].target;
                    t.FindPropertyRelative("fadeDuration").floatValue      = cmds[i].fade;
                    entry.FindPropertyRelative("startTime").floatValue          = cmds[i].start;
                    entry.FindPropertyRelative("duration").floatValue           = dur;
                    entry.FindPropertyRelative("inputBufferDuration").floatValue= cmds[i].buffer;
                }

                // signalTransitions
                var sigs = signalTable.TryGetValue(name, out var sigArr) ? sigArr : System.Array.Empty<SignalEntry>();
                var sigsProp = so.FindProperty("signalTransitions");
                sigsProp.ClearArray();
                sigsProp.arraySize = sigs.Length;
                for (int i = 0; i < sigs.Length; i++)
                {
                    var elem = sigsProp.GetArrayElementAtIndex(i);
                    elem.FindPropertyRelative("signalName").stringValue       = sigs[i].signal;
                    elem.FindPropertyRelative("targetActionName").stringValue = sigs[i].target;
                    elem.FindPropertyRelative("fadeDuration").floatValue      = sigs[i].fade;
                }

                so.ApplyModifiedProperties();

                // 同步建 CommandTransitionTrack，否则 Inspector 打开时 SyncFromTracks 会把列表清空
                RebuildCommandTransitionTracks(action);
                RebuildHitBoxTracks(action, hitBoxTable.TryGetValue(name, out var hbArr) ? hbArr : null);

                EditorUtility.SetDirty(action);
                success++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnbiSetup] 配置完成：{success} 个成功，{missing} 个未找到");
            EditorUtility.DisplayDialog("安比转移配置", $"完成！{success} 个动作已配置。\n\n注意：攻击连招窗口(startTime/duration)使用了默认值，请根据实际动画时长在 Inspector 中微调。", "OK");
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

        private struct SignalEntry
        {
            public string signal;
            public string target;
            public float  fade;
        }

        private static CommandEntry Cmd(EInputCommand command, EInputPhase phase, string target,
                                         float fade, float start, float dur, float buffer)
            => new CommandEntry { command = command, phase = phase, target = target,
                                  fade = fade, start = start, dur = dur, buffer = buffer };

        private static SignalEntry Sig(string signal, string target, float fade = 0.05f)
            => new SignalEntry { signal = signal, target = target, fade = fade };

        private struct HitBoxEntry
        {
            public float start;
            public float end;
            public float damage;
            public float distance;
            public float angle;
        }

        private static HitBoxEntry HBx(float start, float end, float damage, float distance = 2f, float angle = 80f)
            => new HitBoxEntry { start = start, end = end, damage = damage, distance = distance, angle = angle };

        private static void RebuildHitBoxTracks(ActionSO action, HitBoxEntry[] entries)
        {
            foreach (var track in action.GetOutputTracks().OfType<HitBoxTrack>().ToList())
                action.DeleteTrack(track);

            if (entries == null || entries.Length == 0) return;

            foreach (var entry in entries)
            {
                var track = action.CreateTrack<HitBoxTrack>(null, "攻击判定");
                var clip  = track.CreateClip<HitBoxClip>();

                if (clip.asset is HitBoxClip hitBoxClip)
                {
                    hitBoxClip.damage         = entry.damage;
                    hitBoxClip.attackDistance = entry.distance;
                    hitBoxClip.attackAngle    = entry.angle;
                    EditorUtility.SetDirty(hitBoxClip);
                }

                clip.start        = entry.start;
                clip.duration     = System.Math.Max(0.05, entry.end - entry.start);
                clip.displayName  = $"HitBox {entry.damage}dmg";
            }
        }

        /// <summary>
        /// 返回 ActionSO 中 AnimationTrack 的 clip 总时长。
        /// 取所有 clip 的最大 end 时间，读不到则返回 1f。
        /// </summary>
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

        /// <summary>
        /// 根据 commandTransitions 列表重建 Timeline 中的 CommandTransitionTrack，
        /// 与 ActionSOInspector.SyncCommandTransitionTracks 逻辑一致。
        /// </summary>
        private static void RebuildCommandTransitionTracks(ActionSO action)
        {
            // 清除旧轨道
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

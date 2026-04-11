namespace SkillSystem
{
    /// <summary>
    /// 怪物动作名常量 - 与 ActionSO.name / Animator State 名称一一对应
    /// </summary>
    public static class MonsterActionNames
    {
        // 待机 / 移动
        public const string Idle      = "Idle";
        public const string Walk      = "Walk";
        public const string WalkStart = "WalkStart";
        public const string Run       = "Run";
        public const string RunStart  = "RunStart";
        public const string RunEnd    = "RunEnd";

        // 战斗
        public const string Attack01 = "Attack01";
        public const string Attack02 = "Attack02";
        public const string Dodge    = "Dodge";

        // 受击
        public const string HitStay        = "HitStay";
        public const string HitFrontLight  = "HitFrontLight";
        public const string HitFrontHeavy  = "HitFrontHeavy";

        // 死亡
        public const string Dead = "Dead";
    }
}

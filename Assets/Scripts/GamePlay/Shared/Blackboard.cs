using System.Collections.Generic;

/// <summary>
/// 通用键值黑板，供战斗实体共享状态与目标数据。
/// 每个 CharacterBehaviour / MonsterBehaviour 持有一个独立实例。
/// </summary>
public class Blackboard
{
    private readonly Dictionary<string, object> _data = new();

    /// <summary>写入 / 覆盖键值</summary>
    public void Set<T>(string key, T value) => _data[key] = value;

    /// <summary>读取键值；键不存在时返回 defaultValue</summary>
    public T Get<T>(string key, T defaultValue = default)
    {
        return _data.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue;
    }

    /// <summary>尝试读取键值</summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var v) && v is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public void Remove(string key) => _data.Remove(key);
    public bool Has(string key)    => _data.ContainsKey(key);
    public void Clear()            => _data.Clear();
}

/// <summary>
/// 黑板键常量 — 统一管理所有条目名称，避免拼写错误
/// </summary>
public static class BlackboardKeys
{
    /// <summary>当前战斗目标的 Transform</summary>
    public const string Target = "Target";
    /// <summary>当前战斗目标的 CombatEntity</summary>
    public const string TargetCombatEntity = "TargetCombatEntity";
}

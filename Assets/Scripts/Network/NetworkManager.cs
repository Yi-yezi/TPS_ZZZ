using System.Collections.Generic;
using System.Threading.Tasks;
using SkillSystem;
using UnityEngine;
using TpsProtocol;

/// <summary>
/// 网络管理器（Unity 单例）。
///
/// 职责：
///   1. 连接服务器并发送加入请求
///   2. 主线程轮询消息并分发处理
///   3. 20Hz 上报本地玩家状态（位置/旋转/动作）
///   4. 生成/销毁远程玩家 GameObject
///   5. 将 StateSync 快照分发给各 RemotePlayerController
///   6. 处理伤害广播和死亡通知
///
/// 场景中需要一个空 GameObject 挂载此组件，并在 Inspector 中设置：
///   - serverHost / serverPort
///   - playerPrefab（与本地玩家相同的角色预制体，生成远程玩家时自动替换组件）
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("服务器")]
    public string serverHost = "127.0.0.1";
    public int serverPort = 7777;
    public string playerName = "";

    [Header("远程玩家")]
    [Tooltip("角色预制体（与本地玩家相同），生成时自动移除本地组件并添加 RemotePlayerController")]
    public GameObject playerPrefab;

    [Header("怪物")]
    [Tooltip("怪物预制体列表，元素名称需与服务器 MonsterType 对应")]
    public MonsterPrefabEntry[] monsterPrefabs;

    [Header("发送频率")]
    [Tooltip("每秒向服务器发送状态的次数")]
    public int sendRate = 20;

    // ── 状态 ──────────────────────────────────────
    private NetworkClient _client;
    private int _localPlayerId = -1;
    private float _sendTimer;
    private float _sendInterval;

    private readonly Dictionary<int, RemotePlayerController> _remotePlayers = new();
    private readonly Dictionary<int, RemoteMonsterController> _remoteMonsters = new();

    // ── 预测回滚 ──────────────────────────────
    private uint _inputSequence;            // 递增输入序列号
    private uint _lastAckedSequence;        // 服务器最后确认的序列号
    /// <summary>输入历史缓冲：序列号 → (位置, 旋转Y)。</summary>
    private readonly Dictionary<uint, (Vector3 pos, float rotY)> _inputBuffer = new();
    [Header("预测回滚")]
    [Tooltip("位置偏差超过此值时回滚修正")]
    public float reconcileThreshold = 0.5f;

    // ── 断线重连 ──────────────────────────────
    private string _reconnectToken;
    private bool _isReconnecting;
    private float _reconnectTimer;
    private const float ReconnectInterval = 2f;  // 每 2 秒尝试一次
    private const float ReconnectTimeout  = 30f; // 最多尝试 30 秒
    private float _reconnectElapsed;

    // ── 本地玩家引用（由 LocalPlayerSync 设置） ──
    private CharacterBehaviour _localCharacter;
    private CombatEntity _localCombat;

    // ── 公开属性 ──────────────────────────────────
    public int LocalPlayerId => _localPlayerId;
    public bool IsConnected => _client != null && _client.IsConnected;

    // ── 事件（供外部系统监听）─────────────────
    public event System.Action<int> OnJoinedServer;                // 本地玩家成功加入，参数为分配的 PlayerId
    public event System.Action<S2C_DamageEvent> OnDamageReceived;  // 收到伤害广播（可用于 UI 飘字等）
    public event System.Action OnDisconnected;                     // 与服务器断开连接

    // ═══════════════════════════════════════
    //   生命周期
    // ═══════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _sendInterval = 1f / sendRate;
    }

    void Update()
    {
        // ── 断线重连逻辑 ──
        if (_isReconnecting)
        {
            _reconnectElapsed += Time.unscaledDeltaTime;
            if (_reconnectElapsed >= ReconnectTimeout)
            {
                Debug.LogWarning("[Net] Reconnect timed out.");
                _isReconnecting = false;
                OnDisconnected?.Invoke();
                return;
            }

            _reconnectTimer += Time.unscaledDeltaTime;
            if (_reconnectTimer >= ReconnectInterval)
            {
                _reconnectTimer = 0f;
                TryReconnect();
            }

            // 重连期间也要轮询消息
            if (_client != null)
            {
                while (_client.TryGetMessage(out var rmsg))
                    HandleMessage(rmsg);
            }
            return;
        }

        if (_client == null) return;

        // 1. 轮询消息——后台线程读取的消息通过 ConcurrentQueue 传到主线程
        while (_client.TryGetMessage(out var msg))
            HandleMessage(msg);

        // 2. 检测断线 → 启动重连
        if (_client.Disconnected)
        {
            if (!string.IsNullOrEmpty(_reconnectToken) && _localPlayerId >= 0)
            {
                Debug.Log("[Net] Connection lost, attempting reconnect...");
                _isReconnecting = true;
                _reconnectTimer = ReconnectInterval; // 立即尝试第一次
                _reconnectElapsed = 0f;
                _client = null;
                return;
            }

            Debug.Log("[Net] Disconnected from server.");
            OnDisconnected?.Invoke();
            _client = null;
            return;
        }

        // 3. 定时发送本地状态（20Hz）—— 使用 unscaledDeltaTime，暂停菜单时仍保持心跳
        if (_localPlayerId >= 0 && _localCharacter != null)
        {
            _sendTimer += Time.unscaledDeltaTime;
            if (_sendTimer >= _sendInterval)
            {
                _sendTimer = 0f;
                SendLocalState();
            }
        }
    }

    void OnDestroy()
    {
        _client?.Disconnect();
    }

    // ═══════════════════════════════════════
    //   公开接口
    // ═══════════════════════════════════════

    /// <summary>主动断开与服务器的连接。</summary>
    public void Disconnect()
    {
        _isReconnecting = false;
        _client?.Disconnect();
        _client = null;

        // 清理所有远程玩家
        foreach (var kv in _remotePlayers)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        _remotePlayers.Clear();

        // 清理所有远程怪物（先从罗盘移除）
        foreach (var kv in _remoteMonsters)
        {
            if (kv.Value != null)
            {
                if (Compass.Instance != null)
                    Compass.Instance.UnregisterCompassElement(kv.Value.transform);
                Destroy(kv.Value.gameObject);
            }
        }
        _remoteMonsters.Clear();

        // 本地玩家不销毁（场景物体，Cinemachine 相机绑定在上面），仅清理网络引用
        _localPlayerId = -1;
        _inputSequence = 0;
        _lastAckedSequence = 0;
        _inputBuffer.Clear();
        // _reconnectToken 保留，重连时可复用

        OnDisconnected?.Invoke();
    }

    /// <summary>连接服务器并请求加入。</summary>
    public async void Connect()
    {
        _client = new NetworkClient();
        await _client.ConnectAsync(serverHost, serverPort);
        Debug.Log($"[Net] Connected to {serverHost}:{serverPort}");

        _client.Send(new NetMessage
        {
            C2SJoin = new C2S_Join
            {
                PlayerName = string.IsNullOrEmpty(playerName) ? System.Environment.MachineName : playerName
            }
        });
    }

    /// <summary>尝试断线重连。</summary>
    private async void TryReconnect()
    {
        try
        {
            _client?.Disconnect();
            _client = new NetworkClient();
            await _client.ConnectAsync(serverHost, serverPort);
            Debug.Log($"[Net] Reconnected TCP to {serverHost}:{serverPort}, sending token...");

            _client.Send(new NetMessage
            {
                C2SReconnect = new C2S_Reconnect
                {
                    Token = _reconnectToken ?? "",
                    PlayerName = string.IsNullOrEmpty(playerName) ? System.Environment.MachineName : playerName
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.Log($"[Net] Reconnect attempt failed: {ex.Message}");
            _client = null;
        }
    }

    /// <summary>注册本地玩家引用（CharacterBehaviour 调用）。</summary>
    public void RegisterLocalPlayer(CharacterBehaviour character)
    {
        _localCharacter = character;
        _localCombat = character.GetComponent<CombatEntity>();
    }

    /// <summary>
    /// 向服务器报告命中。
    /// 由本地玩家 HitBox 检测到命中远程玩家时调用。
    /// 服务器收到后权威扣血并广播 DamageEvent。
    /// </summary>
    public void SendHitReport(int targetId, float damage, Vector3 hitPoint, Vector3 hitDir)
    {
        if (_client == null || !_client.IsConnected) return;

        _client.Send(new NetMessage
        {
            C2SHitReport = new C2S_HitReport
            {
                TargetId = targetId,
                Damage = damage,
                HitPoint = new Vec3 { X = hitPoint.x, Y = hitPoint.y, Z = hitPoint.z },
                HitDirection = new Vec3 { X = hitDir.x, Y = hitDir.y, Z = hitDir.z }
            }
        });
    }

    /// <summary>向服务器报告命中怪物。</summary>
    public void SendHitMonsterReport(int monsterId, float damage, Vector3 hitPoint, Vector3 hitDir)
    {
        if (_client == null || !_client.IsConnected) return;

        _client.Send(new NetMessage
        {
            C2SHitMonsterReport = new C2S_HitMonsterReport
            {
                MonsterId = monsterId,
                Damage = damage,
                HitPoint = new Vec3 { X = hitPoint.x, Y = hitPoint.y, Z = hitPoint.z },
                HitDirection = new Vec3 { X = hitDir.x, Y = hitDir.y, Z = hitDir.z }
            }
        });
    }

    /// <summary>通过 PlayerId 获取远程玩家控制器。</summary>
    public RemotePlayerController GetRemotePlayer(int playerId)
    {
        _remotePlayers.TryGetValue(playerId, out var rp);
        return rp;
    }

    /// <summary>通过 MonsterId 获取远程怪物控制器。</summary>
    public RemoteMonsterController GetRemoteMonster(int monsterId)
    {
        _remoteMonsters.TryGetValue(monsterId, out var rm);
        return rm;
    }

    // ═══════════════════════════════════════
    //   消息处理
    // ═══════════════════════════════════════

    private void HandleMessage(NetMessage msg)
    {
        switch (msg.PayloadCase)
        {
            case NetMessage.PayloadOneofCase.S2CJoinResult:
                HandleJoinResult(msg.S2CJoinResult);
                break;
            case NetMessage.PayloadOneofCase.S2CPlayerJoined:
                SpawnRemotePlayer(msg.S2CPlayerJoined.Player);
                break;
            case NetMessage.PayloadOneofCase.S2CPlayerLeft:
                RemoveRemotePlayer(msg.S2CPlayerLeft.PlayerId);
                break;
            case NetMessage.PayloadOneofCase.S2CStateSync:
                HandleStateSync(msg.S2CStateSync);
                break;
            case NetMessage.PayloadOneofCase.S2CDamageEvent:
                HandleDamage(msg.S2CDamageEvent);
                break;
            case NetMessage.PayloadOneofCase.S2CPlayerDied:
                HandlePlayerDied(msg.S2CPlayerDied);
                break;
            case NetMessage.PayloadOneofCase.S2CMonsterSync:
                HandleMonsterSync(msg.S2CMonsterSync);
                break;
            case NetMessage.PayloadOneofCase.S2CMonsterDamageEvent:
                HandleMonsterDamage(msg.S2CMonsterDamageEvent);
                break;
            case NetMessage.PayloadOneofCase.S2CMonsterDied:
                HandleMonsterDied(msg.S2CMonsterDied);
                break;
            case NetMessage.PayloadOneofCase.S2CMonsterSpawned:
                SpawnRemoteMonster(msg.S2CMonsterSpawned.Monster);
                break;
            case NetMessage.PayloadOneofCase.S2CReconnectResult:
                HandleReconnectResult(msg.S2CReconnectResult);
                break;
        }
    }

    private void HandleJoinResult(S2C_JoinResult result)
    {
        if (result.PlayerId < 0)
        {
            Debug.LogWarning("[Net] Server full, cannot join.");
            return;
        }

        _localPlayerId = result.PlayerId;
        _reconnectToken = result.ReconnectToken;
        Debug.Log($"[Net] Joined as Player {_localPlayerId} (token={_reconnectToken?.Substring(0, 8)}...)");

        // 生成已有的远程玩家
        foreach (var info in result.ExistingPlayers)
            SpawnRemotePlayer(info);

        // 销毁场景中的本地怪物，改用服务器驱动
        DestroyLocalMonsters();

        // 生成服务器通知的怪物
        foreach (var monsterInfo in result.ExistingMonsters)
            SpawnRemoteMonster(monsterInfo);

        OnJoinedServer?.Invoke(_localPlayerId);
    }

    /// <summary>
    /// 处理 StateSync：差分更新 + 预测回滚。
    /// 只包含有变化的玩家快照（差分压缩），跳过本地玩家。
    /// 通过 ack_position 与本地预测位置比较，偏差大时修正。
    /// </summary>
    private void HandleStateSync(S2C_StateSync sync)
    {
        foreach (var snapshot in sync.Players)
        {
            // 跳过自己
            if (snapshot.PlayerId == _localPlayerId) continue;

            if (_remotePlayers.TryGetValue(snapshot.PlayerId, out var rp))
                rp.ApplySnapshot(snapshot);
        }

        // ── 预测回滚：服务器 ACK 位置校验 ──
        if (sync.LastProcessedSequence > 0 && _localCharacter != null && sync.AckPosition != null)
        {
            uint acked = sync.LastProcessedSequence;
            Vector3 serverPos = new Vector3(sync.AckPosition.X, sync.AckPosition.Y, sync.AckPosition.Z);

            // 比较服务器确认位置与该序列号时客户端记录的位置
            // 如果两者一致，说明客户端预测正确，无需修正
            if (_inputBuffer.TryGetValue(acked, out var recorded))
            {
                float drift = Vector3.Distance(serverPos, recorded.pos);
                if (drift > reconcileThreshold)
                {
                    // 计算修正量：服务器位置与当时记录位置的差值
                    Vector3 correction = serverPos - recorded.pos;
                    _localCharacter.transform.position += correction;
                    Debug.LogWarning($"[Net] Reconcile: drift={drift:F2}m at seq={acked}, applied correction.");
                }
            }

            // 清理已确认的输入历史
            if (acked > _lastAckedSequence)
            {
                for (uint seq = _lastAckedSequence + 1; seq <= acked; seq++)
                    _inputBuffer.Remove(seq);
                _lastAckedSequence = acked;
            }
        }
    }

    /// <summary>
    /// 处理伤害广播。
    /// 如果目标是本地玩家 → 调用 CombatEntity.TakeDamage 触发受击动画/音效。
    /// 如果目标是远程玩家 → 远程玩家的受击动作由 StateSync 中的 ActionName 变化驱动。
    /// </summary>
    private void HandleDamage(S2C_DamageEvent evt)
    {
        // 如果目标是本地玩家，应用伤害
        if (evt.TargetId == _localPlayerId && _localCombat != null)
        {
            _localCombat.TakeDamage(new DamageInfo
            {
                Damage = evt.Damage,
                HitPoint = new Vector3(evt.HitPoint.X, evt.HitPoint.Y, evt.HitPoint.Z),
                HitDirection = new Vector3(evt.HitDirection.X, evt.HitDirection.Y, evt.HitDirection.Z)
            });
        }

        OnDamageReceived?.Invoke(evt);
    }

    private void HandlePlayerDied(S2C_PlayerDied died)
    {
        if (died.PlayerId == _localPlayerId) return; // 本地玩家死亡由自身处理

        if (_remotePlayers.TryGetValue(died.PlayerId, out var remotePlayer))
            remotePlayer.OnDied();
    }

    /// <summary>
    /// 处理断线重连结果。
    /// 成功：恢复所有远程实体 + 本地玩家位置。
    /// 失败：直接触发 OnDisconnected 让上层处理（回主菜单等）。
    /// </summary>
    private void HandleReconnectResult(S2C_ReconnectResult result)
    {
        _isReconnecting = false;

        if (!result.Success)
        {
            Debug.LogWarning("[Net] Reconnect failed (token expired or invalid).");
            _reconnectToken = null;
            _localPlayerId = -1;
            OnDisconnected?.Invoke();
            return;
        }

        _localPlayerId = result.PlayerId;
        Debug.Log($"[Net] Reconnected as Player {_localPlayerId}");

        // 清理所有旧的远程实体
        foreach (var kv in _remotePlayers)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        _remotePlayers.Clear();

        foreach (var kv in _remoteMonsters)
        {
            if (kv.Value != null)
            {
                if (Compass.Instance != null)
                    Compass.Instance.UnregisterCompassElement(kv.Value.transform);
                Destroy(kv.Value.gameObject);
            }
        }
        _remoteMonsters.Clear();

        // 恢复本地玩家位置
        if (_localCharacter != null && result.Position != null)
        {
            _localCharacter.transform.position = new Vector3(
                result.Position.X, result.Position.Y, result.Position.Z);
        }

        // 恢复本地玩家 HP
        if (_localCombat != null)
            _localCombat.SetHp(result.Hp, result.MaxHp);

        // 重新生成远程玩家
        foreach (var info in result.ExistingPlayers)
            SpawnRemotePlayer(info);

        // 销毁场景中残留的怪物，改用新同步的
        DestroyLocalMonsters();
        foreach (var monsterInfo in result.ExistingMonsters)
            SpawnRemoteMonster(monsterInfo);

        // 清理输入缓冲
        _inputBuffer.Clear();
        _lastAckedSequence = 0;
        _inputSequence = 0;

        OnJoinedServer?.Invoke(_localPlayerId);
    }

    // ── 远程玩家管理 ──────────────────────────────

    /// <summary>
    /// 生成远程玩家。
    /// 使用与本地玩家相同的预制体，实例化后：
    ///   1. 从 CharacterBehaviour 读取 actionList
    ///   2. 移除本地组件（CharacterBehaviour、CombatEntity、CharacterController 等）
    ///   3. 添加 RemotePlayerController 并初始化
    /// </summary>
    private void SpawnRemotePlayer(PlayerInfo info)
    {
        if (info.PlayerId == _localPlayerId) return;
        if (_remotePlayers.ContainsKey(info.PlayerId)) return;
        if (playerPrefab == null)
        {
            Debug.LogWarning("[Net] playerPrefab not assigned!");
            return;
        }

        var spawnPosition = new Vector3(info.Position.X, info.Position.Y, info.Position.Z);
        var spawnRotation = Quaternion.Euler(0, info.RotationY, 0);
        var playerObject = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        playerObject.name = $"RemotePlayer_{info.PlayerId}_{info.PlayerName}";

        // 从本地组件读取 actionList
        var localBehaviour = playerObject.GetComponent<CharacterBehaviour>();
        ActionListSO actionList = localBehaviour != null ? localBehaviour.actionList : null;

        // 移除本地玩家组件（顺序重要：先移除依赖者）
        if (localBehaviour != null) Destroy(localBehaviour);
        var combatEntity = playerObject.GetComponent<CombatEntity>();
        if (combatEntity != null) Destroy(combatEntity);

        // 保留碰撞体积：CharacterController → CapsuleCollider
        ReplaceCharacterControllerWithCapsule(playerObject);

        // 添加远程玩家控制器
        var remotePlayer = playerObject.AddComponent<RemotePlayerController>();
        remotePlayer.actionList = actionList;
        remotePlayer.Init(info.PlayerId, info.Hp, info.MaxHp);

        _remotePlayers[info.PlayerId] = remotePlayer;
        Debug.Log($"[Net] Spawned remote player {info.PlayerName} (id={info.PlayerId})");
    }

    private void RemoveRemotePlayer(int playerId)
    {
        if (_remotePlayers.TryGetValue(playerId, out var rp))
        {
            _remotePlayers.Remove(playerId);
            Destroy(rp.gameObject);
            Debug.Log($"[Net] Removed remote player id={playerId}");
        }
    }

    // ── 发送本地状态 ──────────────────────────────

    /// <summary>
    /// 发送本地玩家状态到服务器（20Hz）。
    /// 包含：Root Motion 后的位置、Y 旋转、当前动作名 + 播放时间 + 序列号。
    /// 同时将输入存入缓冲，供预测回滚时比对。
    /// </summary>
    private void SendLocalState()
    {
        var position = _localCharacter.transform.position;
        var actionDriver = _localCharacter.ActionDriverRef;

        _inputSequence++;

        _client.Send(new NetMessage
        {
            C2SPlayerState = new C2S_PlayerState
            {
                Position = new Vec3 { X = position.x, Y = position.y, Z = position.z },
                RotationY = _localCharacter.transform.eulerAngles.y,
                ActionName = actionDriver?.CurrentActionName ?? "Idle",
                ActionTime = actionDriver?.ActionTime ?? 0f,
                Sequence = _inputSequence
            }
        });

        // 存入输入缓冲（供回滚比对）
        _inputBuffer[_inputSequence] = (position, _localCharacter.transform.eulerAngles.y);

        // 清理过老的输入历史（保留最多 200 帧 = 10 秒 @20Hz）
        if (_inputBuffer.Count > 200)
        {
            uint oldest = _inputSequence - 200;
            var toRemove = new System.Collections.Generic.List<uint>();
            foreach (var key in _inputBuffer.Keys)
                if (key < oldest) toRemove.Add(key);
            foreach (var key in toRemove)
                _inputBuffer.Remove(key);
        }
    }

    // ── 怪物管理 ──────────────────────────────

    /// <summary>
    /// 销毁场景中所有带 "Enemy" Tag 的本地怪物（连接服务器后改用服务器驱动）。
    /// </summary>
    private void DestroyLocalMonsters()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var go in enemies)
        {
            Debug.Log($"[Net] Destroying local monster: {go.name}");
            Destroy(go);
        }
    }

    private void SpawnRemoteMonster(MonsterInfo info)
    {
        if (_remoteMonsters.ContainsKey(info.MonsterId)) return;

        var prefab = FindMonsterPrefab(info.MonsterType);
        if (prefab == null)
        {
            Debug.LogWarning($"[Net] Monster prefab not found for type: {info.MonsterType}");
            return;
        }

        var spawnPosition = new Vector3(info.Position.X, info.Position.Y, info.Position.Z);
        var spawnRotation = Quaternion.Euler(0, info.RotationY, 0);
        var monsterObject = Instantiate(prefab, spawnPosition, spawnRotation);
        monsterObject.name = $"RemoteMonster_{info.MonsterId}";
        monsterObject.tag = "Enemy";

        // 从本地 MonsterBehaviour 读取配置后移除（改用服务器驱动的 RemoteMonsterController）
        var localMonsterBehaviour = monsterObject.GetComponent<MonsterBehaviour>();
        ActionListSO actionList = localMonsterBehaviour != null ? localMonsterBehaviour.actionList : null;
        AudioClip[] hitSfxClips = localMonsterBehaviour != null ? localMonsterBehaviour.hitSfxClips : null;
        float hitSfxVolume = localMonsterBehaviour != null ? localMonsterBehaviour.hitSfxVolume : 0.7f;
        if (localMonsterBehaviour != null) Destroy(localMonsterBehaviour);

        // CombatEntity 保留 —— WorldspaceHealthBar 等 UI 依赖它
        // CharacterController → CapsuleCollider（保持碰撞体积，防止玩家穿模）
        ReplaceCharacterControllerWithCapsule(monsterObject);

        var combatEntity = monsterObject.GetComponent<CombatEntity>();
        var remoteMonster = monsterObject.AddComponent<RemoteMonsterController>();
        remoteMonster.actionList = actionList;
        remoteMonster.hitSfxClips = hitSfxClips;
        remoteMonster.hitSfxVolume = hitSfxVolume;
        remoteMonster.Init(info.MonsterId, info.Hp, info.MaxHp, combatEntity);

        _remoteMonsters[info.MonsterId] = remoteMonster;

        // 注册到罗盘
        if (Compass.Instance != null)
            Compass.Instance.RegisterEnemy(monsterObject.transform);

        Debug.Log($"[Net] Spawned remote monster {info.MonsterType} (id={info.MonsterId})");
    }

    private void HandleMonsterSync(S2C_MonsterSync sync)
    {
        foreach (var snapshot in sync.Monsters)
        {
            if (_remoteMonsters.TryGetValue(snapshot.MonsterId, out var remoteMonster))
                remoteMonster.ApplySnapshot(snapshot);
        }
    }

    private void HandleMonsterDamage(S2C_MonsterDamageEvent evt)
    {
        if (_remoteMonsters.TryGetValue(evt.MonsterId, out var remoteMonster))
        {
            remoteMonster.OnDamaged(
                evt.Damage,
                new Vector3(evt.HitPoint.X, evt.HitPoint.Y, evt.HitPoint.Z),
                new Vector3(evt.HitDirection.X, evt.HitDirection.Y, evt.HitDirection.Z),
                evt.HitSignal
            );
        }
    }

    private void HandleMonsterDied(S2C_MonsterDied died)
    {
        if (_remoteMonsters.TryGetValue(died.MonsterId, out var remoteMonster))
        {
            // 从罗盘移除
            if (Compass.Instance != null)
                Compass.Instance.UnregisterCompassElement(remoteMonster.transform);

            remoteMonster.OnDied();
            _remoteMonsters.Remove(died.MonsterId);
            // 延迟销毁，让死亡动画播放完
            Destroy(remoteMonster.gameObject, 2f);
        }
    }

    /// <summary>
    /// 将 CharacterController 替换为同尺寸的 CapsuleCollider。
    /// 远程实体不需要 CharacterController 的移动逻辑，但需保留碰撞体积。
    /// </summary>
    private static void ReplaceCharacterControllerWithCapsule(GameObject target)
    {
        var characterController = target.GetComponent<CharacterController>();
        if (characterController == null) return;

        float height = characterController.height;
        float radius = characterController.radius;
        Vector3 center = characterController.center;
        Destroy(characterController);

        var capsuleCollider = target.AddComponent<CapsuleCollider>();
        capsuleCollider.height = height;
        capsuleCollider.radius = radius;
        capsuleCollider.center = center;
    }

    private GameObject FindMonsterPrefab(string monsterType)
    {
        if (monsterPrefabs == null) return null;
        foreach (var entry in monsterPrefabs)
        {
            if (entry.monsterType == monsterType)
                return entry.prefab;
        }
        // 如果只有一个预制体，默认使用它
        return monsterPrefabs.Length == 1 ? monsterPrefabs[0].prefab : null;
    }
}

/// <summary>
/// 怪物预制体配置条目（Inspector 中配置）。
/// </summary>
[System.Serializable]
public class MonsterPrefabEntry
{
    [Tooltip("对应服务器 MonsterType 字段")]
    public string monsterType = "Monster01";
    public GameObject prefab;
}

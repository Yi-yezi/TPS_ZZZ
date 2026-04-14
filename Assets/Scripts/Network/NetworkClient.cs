using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using TpsProtocol;

/// <summary>
/// 底层 TCP 网络客户端（纯 C# 类，不继承 MonoBehaviour）。
///
/// 架构：
///   - ConnectAsync()   → 建立 TCP 连接，启动后台读线程
///   - 后台线程          → ReadLoopAsync 持续读取帧，解析 protobuf，放入 ConcurrentQueue
///   - 主线程 TryGetMessage → 从队列取消息（无锁，Unity Update 中调用）
///   - Send()           → 主线程同步写入（发送频率低，不需要异步写队列）
///
/// 帧协议（与服务器 ClientSession 对称）：
///   [4 字节大端长度] + [protobuf NetMessage 序列化字节]
/// </summary>
public class NetworkClient
{
    /// <summary>单条消息最大体积，与服务器一致。</summary>
    private const int MaxMessageSize = 64 * 1024;

    private TcpClient _tcp;
    private NetworkStream _stream;
    private CancellationTokenSource _cts;

    private readonly ConcurrentQueue<NetMessage> _received = new();
    private volatile bool _disconnected;

    public bool IsConnected => _tcp != null && _tcp.Connected && !_disconnected;

    /// <summary>
    /// 连接服务器。成功后自动启动后台读取线程。
    /// 在 NetworkManager.Connect() 中调用。
    /// </summary>
    public async Task ConnectAsync(string host, int port)
    {
        _tcp = new TcpClient { NoDelay = true }; // 关闭 Nagle 减少延迟
        await _tcp.ConnectAsync(host, port);
        _stream = _tcp.GetStream();
        _cts = new CancellationTokenSource();

        // 在线程池启动读取循环，避免阻塞主线程
        _ = Task.Run(() => ReadLoopAsync());
    }

    /// <summary>
    /// 主线程调用，轮询收到的消息。
    /// </summary>
    public bool TryGetMessage(out NetMessage msg) => _received.TryDequeue(out msg);

    /// <summary>
    /// 是否已断开连接。
    /// </summary>
    public bool Disconnected => _disconnected;

    /// <summary>
    /// 发送消息（主线程调用）。
    /// 将 NetMessage 序列化为 [4B 长度 + payload] 帧，同步写入 socket。
    /// 发送频率 20Hz，单帧数据量小，同步写入不会阻塞。
    /// </summary>
    public void Send(NetMessage msg)
    {
        if (_stream == null || _disconnected) return;

        try
        {
            var payload = msg.ToByteArray();
            var frame = new byte[4 + payload.Length];
            int len = payload.Length;
            frame[0] = (byte)(len >> 24);
            frame[1] = (byte)(len >> 16);
            frame[2] = (byte)(len >> 8);
            frame[3] = (byte)len;
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            _stream.Write(frame, 0, frame.Length);
        }
        catch (Exception)
        {
            _disconnected = true;
        }
    }

    /// <summary>
    /// 断开连接。
    /// </summary>
    public void Disconnect()
    {
        _disconnected = true;
        _cts?.Cancel();
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
    }

    // ── 后台读取循环 ──────────────────────────────    // 在线程池中运行，读帧 → 解析 protobuf → 入队到 ConcurrentQueue
    // 主线程通过 TryGetMessage() 无锁取得
    private async Task ReadLoopAsync()
    {
        var header = new byte[4];

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (!await ReadExactAsync(header, 4)) break;

                int len = (header[0] << 24) | (header[1] << 16) |
                          (header[2] << 8)  |  header[3];

                if (len <= 0 || len > MaxMessageSize) break;

                var payload = new byte[len];
                if (!await ReadExactAsync(payload, len)) break;

                var msg = NetMessage.Parser.ParseFrom(payload);
                _received.Enqueue(msg);
            }
        }
        catch (Exception) { /* 连接关闭 */ }

        _disconnected = true;
    }

    private async Task<bool> ReadExactAsync(byte[] buf, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await _stream.ReadAsync(buf, offset, count - offset, _cts.Token);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }
}

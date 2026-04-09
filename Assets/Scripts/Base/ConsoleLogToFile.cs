using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 捕获Unity控制台日志，异步写入持久化目录的日志文件
/// 日志文件路径：Application.persistentDataPath/UnityGameLog_yyyyMMdd.log
/// </summary>
public class ConsoleLogToFile : MonoBehaviour
{
    // 日志文件写入流（全局单例保持打开，提升性能）
    private StreamWriter _logWriter;
    // 日志文件路径
    private string _logFilePath;
    
    // 单例实例，保证全局唯一
    public static ConsoleLogToFile Instance { get; private set; }

    private void Awake()
    {
        // 单例模式，防止重复创建
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 切换场景不销毁

        // 初始化日志文件
        InitializeLogFile();
        
        // 订阅Unity控制台日志事件
        Application.logMessageReceivedThreaded += OnLogReceived;
    }

    /// <summary>
    /// 初始化日志文件，创建文件并写入头部信息
    /// </summary>
    private void InitializeLogFile()
    {
        // 按日期生成日志文件名，避免单个文件过大
        string logFileName = $"UnityGameLog_{DateTime.Now:yyyyMMdd}.log";
        // 持久化目录（不同平台自动适配：Windows/Mac/Android/iOS）
        _logFilePath = Path.Combine(Application.persistentDataPath, logFileName);

        try
        {
            // 创建文件流，共享读写，异步写入
            FileStream fileStream = new FileStream(
                _logFilePath, 
                FileMode.OpenOrCreate,          // 追加模式，不存在则创建
                FileAccess.Write, 
                FileShare.Read, 
                4096,                     // 缓冲区大小
                useAsync: true);          // 启用异步IO

            // 初始化StreamWriter，使用UTF8编码
            _logWriter = new StreamWriter(fileStream, Encoding.UTF8);
            _logWriter.AutoFlush = false; // 关闭自动刷新，提升性能

            // 写入日志开头标记
            WriteLogHeader();
        }
        catch (Exception e)
        {
            Debug.LogError($"日志文件初始化失败：{e.Message}");
        }
    }

    /// <summary>
    /// 写入日志文件头部（启动时间）
    /// </summary>
    private void WriteLogHeader()
    {
        string header = $"==================== 游戏启动 - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====================\n";
        _logWriter.WriteAsync(header);
    }

    /// <summary>
    /// 日志接收回调（Unity多线程安全的回调）
    /// </summary>
    private void OnLogReceived(string logText, string stackTrace, LogType type)
    {
        // 过滤空日志
        if (string.IsNullOrEmpty(logText)) return;

        // 格式化日志：时间 + 类型 + 内容
        string log = $"[{DateTime.Now:HH:mm:ss}] [{type}] {logText}\n";

        // 如果是错误/异常，追加堆栈信息
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            log += $"堆栈信息：{stackTrace}\n";
        }

        // 异步写入文件（核心：不阻塞主线程）
        WriteLogAsync(log);
    }

    /// <summary>
    /// 异步写入日志到文件
    /// </summary>
    private async void WriteLogAsync(string log)
    {
        if (_logWriter == null) return;

        try
        {
            // 异步写入，await 不会阻塞主线程
            await _logWriter.WriteAsync(log);
            // 手动批量刷新，比自动刷新性能更高
            await _logWriter.FlushAsync();
        }
        catch (Exception e)
        {
            // 写入失败时输出到控制台（避免死循环，这里不递归写入）
            Debug.LogError($"日志写入失败：{e.Message}");
        }
    }

    /// <summary>
    /// 游戏退出/销毁时，关闭文件流
    /// </summary>
    private void OnDestroy()
    {
        // 取消订阅事件
        Application.logMessageReceivedThreaded -= OnLogReceived;

        // 安全关闭文件流
        if (_logWriter != null)
        {
            _logWriter.FlushAsync().Wait(); // 确保剩余日志写入完成
            _logWriter.Close();
            _logWriter.Dispose();
        }
        
        Instance = null;
    }
}
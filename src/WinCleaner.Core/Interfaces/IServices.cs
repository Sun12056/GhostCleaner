using WinCleaner.Core.Models;

namespace WinCleaner.Core.Interfaces;

/// <summary>操作日志服务。</summary>
public interface ILogService
{
    void Log(LogLevel level, string category, string action, string path, long sizeBytes, string result, string message);

    void Info(string category, string action, string message, string path = "", long sizeBytes = 0);

    void Warning(string category, string action, string message, string path = "", long sizeBytes = 0);

    void Error(string category, string action, string message, string path = "", long sizeBytes = 0);

    Task<IReadOnlyList<LogEntry>> LoadAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    string LogDirectory { get; }
}

/// <summary>配置服务。</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    string SettingsFilePath { get; }
}

/// <summary>系统还原点服务。</summary>
public interface ISystemRestoreService
{
    /// <summary>尝试创建还原点；失败不影响主流程，返回是否成功。</summary>
    Task<bool> TryCreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}

/// <summary>报告导出（JSON / CSV）。</summary>
public interface IReportExporter
{
    Task<string> ExportScanReportAsync(IEnumerable<OrphanItem> items, string filePath, CancellationToken cancellationToken = default);

    Task<string> ExportCleanReportAsync(IEnumerable<LogEntry> entries, string filePath, CancellationToken cancellationToken = default);

    Task<string> ExportQuarantineReportAsync(IEnumerable<QuarantineEntry> entries, string filePath, CancellationToken cancellationToken = default);
}

/// <summary>无效注册表扫描与清理。</summary>
public interface IRegistryCleaner
{
    Task<IReadOnlyList<RegistryIssue>> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task<string> BackupAsync(IEnumerable<RegistryIssue> issues, CancellationToken cancellationToken = default);

    Task<int> RemoveAsync(IEnumerable<RegistryIssue> issues, CancellationToken cancellationToken = default);
}

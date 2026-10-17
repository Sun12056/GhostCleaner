using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhostCleaner.App.Services;

/// <summary>最近一次扫描的摘要（供概览页展示）。</summary>
public sealed class LastScanSummary
{
    public DateTime FinishedAt { get; set; }

    public double DurationSeconds { get; set; }

    public int RootPathCount { get; set; }

    public string RootPathsText { get; set; } = string.Empty;

    public int OrphanCount { get; set; }

    public long TotalSize { get; set; }

    public int HighConfidenceCount { get; set; }

    public int ReviewCount { get; set; }

    public int ProtectedCount { get; set; }
}

/// <summary>
/// UI 层轻量状态存储（%LOCALAPPDATA%\GhostCleaner\ui.json）。
///
/// 说明：概览页需要"上次扫描"的摘要，但 Core 目前还没有扫描结果持久化
/// （UDR-02 IScanHistoryService）。这里在 UI 层保存一份只读摘要作为过渡，
/// UDR-02 落地后应改为读取 Core 的 IScanHistoryService，并删除本类。
/// </summary>
public sealed class UiStateService
{
    private readonly string _path;

    public UiStateService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GhostCleaner");

        _path = Path.Combine(dir, "ui.json");
        UiState = Load();
    }

    public UiState UiState { get; private set; }

    public void UpdateAndSave(Action<UiState> mutate)
    {
        mutate(UiState);
        Save();
    }

    private UiState Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<UiState>(File.ReadAllText(_path)) ?? new UiState();
        }
        catch
        {
            // 读不到就用默认值
        }

        return new UiState();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonSerializer.Serialize(UiState));
        }
        catch
        {
            // 保存失败不影响使用
        }
    }
}

public sealed class UiState
{
    /// <summary>主题模式（由 ThemeService 写入）。</summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; set; }

    /// <summary>最近一次扫描摘要。</summary>
    [JsonPropertyName("lastScan")]
    public LastScanSummary? LastScan { get; set; }
}

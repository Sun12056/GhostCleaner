using System.Globalization;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GhostCleaner.Core.Interfaces;
using GhostCleaner.Core.Models;
using GhostCleaner.Registry;
using GhostCleaner.Scanner;

namespace GhostCleaner.Validate;

/// <summary>
/// Core 0.1 真实环境验证入口（只读）。
///
/// 职责：在真实 Windows 上运行一次完整扫描，导出可供人工回标的 JSONL。
///
/// 安全约束（硬性）：
/// 1. 只读取文件系统与注册表，不写任何业务数据；
/// 2. 本工程不引用 GhostCleaner.Services，因此 CleanupService / QuarantineManager
///    在编译期就不可达，不可能执行删除、隔离或注册表写入；
/// 3. 唯一写操作是输出 JSONL 报告与本地路径映射表。
/// </summary>
internal static class Program
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=========================================================");
        Console.WriteLine(" GhostCleaner Core 0.1 —— 真实环境验证工具");
        Console.WriteLine(" READ-ONLY MODE：只扫描、只导出报告，绝不删除/隔离/写注册表");
        Console.WriteLine("=========================================================");
        Console.WriteLine();

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        var options = ParseArgs(args);
        if (options.Error != null)
        {
            Console.Error.WriteLine("参数错误：" + options.Error);
            PrintUsage();
            return 2;
        }

        var started = DateTimeOffset.Now;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var scanOptions = new ScanOptions
        {
            RootPaths = options.Roots,
            MaxDepth = options.MaxDepth,
            SkipReparsePoints = !options.FollowReparsePoints,
        };

        IAssociationIndex index = new AssociationIndex();
        var inspector = new SoftwareDirectoryInspector();
        var scanner = new OrphanScanner(inspector, index);

        Console.WriteLine($"扫描根路径：{string.Join(" , ", options.Roots)}");
        Console.WriteLine($"最大探测深度：{options.MaxDepth}   包含全部候选：{options.IncludeAll}");
        Console.WriteLine();

        try
        {
            var outcome = await scanner.ScanWithOutcomeAsync(
                scanOptions,
                new Progress<ScanProgress>(p =>
                {
                    if (!string.IsNullOrEmpty(p.Message)) Console.WriteLine("  · " + p.Message);
                }));

            sw.Stop();

            var results = outcome.Items;
            var health = outcome.SourceHealth;
            PrintHealth(outcome);

            // --include-all：绕过 OrphanScanner 的 IsLikelySoftwareDirectory 过滤，
            // 让"空目录 / 用户数据目录 / 普通文件夹"这类负样本也能进入回标集合（TN 统计需要）。
            var rows = new List<LabelRecord>();
            if (options.IncludeAll)
            {
                var candidates = ScanTargetResolver.Resolve(scanOptions);
                Console.WriteLine($"候选目录（含非软件目录）：{candidates.Count}");
                int n = 0;
                foreach (var dir in candidates)
                {
                    if (options.Limit > 0 && n >= options.Limit) break;
                    var info = await inspector.InspectAsync(dir, scanOptions);
                    if (info == null) continue;
                    var hits = index.FindHits(dir);
                    var item = RiskEvaluator.Evaluate(info, hits, scanOptions, health);
                    rows.Add(ToRecord(item, health, options));
                }
            }
            else
            {
                int n = 0;
                foreach (var item in results)
                {
                    if (options.Limit > 0 && n >= options.Limit) break;
                    rows.Add(ToRecord(item, health, options));
                }
            }

            WriteOutputs(rows, options, started, sw.ElapsedMilliseconds);

            Console.WriteLine();
            Console.WriteLine($"共 {rows.Count} 条记录已写出。");
            Console.WriteLine("下一步：人工为每条记录填写 groundTruth / groundTruthConfidence 后再统计指标。");
            Console.WriteLine("READ-ONLY MODE：本次运行未修改任何被扫描内容。");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("已取消。");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("扫描失败：" + ex.Message);
            return 1;
        }
    }

    // ---------------- 输出 ----------------

    private static void WriteOutputs(List<LabelRecord> rows, CmdOptions options, DateTimeOffset started, long elapsedMs)
    {
        foreach (var r in rows)
        {
            r.ScannedAt = started;
            r.ScanDurationMs = elapsedMs;
        }

        var outPath = System.IO.Path.GetFullPath(options.Out);
        var dir = System.IO.Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using (var writer = new StreamWriter(outPath, false, new System.Text.UTF8Encoding(false)))
        {
            foreach (var r in rows)
                writer.WriteLine(JsonSerializer.Serialize(r, JsonOptions));
        }

        Console.WriteLine();
        Console.WriteLine("JSONL 报告：" + outPath);

        // 脱敏时把真实路径另存为本地映射表（不入库）。
        if (options.Redact)
        {
            var mapPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(outPath)!,
                System.IO.Path.GetFileNameWithoutExtension(outPath) + ".path-map.local.json");

            var map = rows.ToDictionary(r => r.SampleId, r => r.RealPath);
            File.WriteAllText(mapPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("路径映射表（仅本地，勿入库）：" + mapPath);
        }

        // 便于快速浏览的控制台摘要
        Console.WriteLine();
        Console.WriteLine("置信度最高的前 15 条：");
        Console.WriteLine("  ID      置信  等级            风险    建议            路径");
        foreach (var r in rows.OrderByDescending(x => x.Core.ConfidenceScore).Take(15))
        {
            Console.WriteLine($"  {r.SampleId}  {r.Core.ConfidenceScore,3}  {r.Core.ConfidenceLevel,-16}{r.Core.Risk,-7}{r.Core.Recommendation,-15}{r.ScannedPath}");
        }
    }

    /// <summary>
    /// 由<b>路径</b>派生的稳定样本 ID。
    ///
    /// <para>为什么不用扫描顺序编号：实测两轮扫描之间 <b>37/47 条</b> 的
    /// <c>U-0xx</c> 发生了错位 —— 扫描顺序与权限、枚举顺序相关，并不稳定。
    /// 人工回标若按 ID 关联会大面积错位，指标全部作废
    /// （见 <c>docs/core-0.1/08-…改动方案.md</c> §6.6）。</para>
    ///
    /// <para>用路径派生后，同一目录在任何一轮扫描、任何扫描顺序下都拿到同一个 ID，
    /// 补样本时也不会与既有回标错位。</para>
    /// </summary>
    private static string StableSampleId(string sampleClass, string realPath)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(realPath.ToLowerInvariant()));

        // 取前 4 字节：数十条样本量下碰撞概率约 3e-5，可忽略；
        // 同时 ID 长度仍可接受（形如 U-1A2B3C4D）。
        var code = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        return $"{sampleClass}-{code:X8}";
    }

    private static LabelRecord ToRecord(OrphanItem item, IReadOnlyList<AssociationSourceHealth> health, CmdOptions options)
    {
        var realPath = item.Path;

        return new LabelRecord
        {
            SampleId = StableSampleId(options.SampleClass, realPath),
            SampleClass = options.SampleClass,
            RealPath = realPath,
            ScannedPath = options.Redact ? Redact(realPath) : realPath,
            PathRedacted = options.Redact,

            // 以下字段留给人工填写
            GroundTruth = null,
            GroundTruthConfidence = null,
            IsActuallyInUse = null,
            ContainsUserData = item.HasUserData,
            HumanNote = null,

            Core = new CoreVerdict
            {
                ConfidenceScore = item.ConfidenceScore,
                ConfidenceLevel = item.ConfidenceLevel.ToString(),
                RiskScore = item.RiskScore,
                Risk = item.Risk.ToString(),
                Recommendation = item.Recommendation.ToString(),
                IsProtected = item.IsProtected,
                ProtectionReason = item.ProtectionReason,
                IsExcluded = item.IsExcluded,
                ExcludeReason = item.ExcludeReason,
                DirectoryCategory = item.CategoryText,
                IsRunning = item.IsRunning,
            },

            Evidence = item.Evidence.Select(e => new EvidenceRow
            {
                Type = e.Type.ToString(),
                Score = e.Score,
                IsPositive = e.IsPositive,
                Severity = e.Severity.ToString(),
                Title = e.Title,
                Description = e.Description,
            }).ToList(),

            Snapshot = new SnapshotRow
            {
                SizeBytes = item.SizeBytes,
                FileCount = item.FileCount,
                DirectoryCount = item.DirectoryCount,
                LastModified = item.LastModified.ToString("o"),
                MainExecutables = item.MainExecutables.ToList(),
            },

            Warnings = item.Warnings.Select(w => w.Message).ToList(),
            WarningCodes = item.Warnings.Select(w => w.Code.ToString()).ToList(),

            AssociationSourceHealth = health.Select(h => new HealthRow
            {
                SourceName = h.SourceName,
                Status = h.Status.ToString(),
                ItemCount = h.ItemCount,
                ErrorMessage = h.ErrorMessage,
            }).ToList(),

            Environment = new EnvironmentRow
            {
                OsVersion = System.Environment.OSVersion.VersionString,
                CoreVersion = "0.1",
                GitCommit = options.GitCommit,
                RunAsAdmin = IsAdministrator(),
            },
        };
    }

    // ---------------- 脱敏 ----------------

    private static string Redact(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var profile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile) &&
            path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
        {
            return "<USER>" + path.Substring(profile.Length);
        }

        // 兜底：任何盘符下的 \Users\<name>\ 一律替换
        return Regex.Replace(path, @"(?i)^([A-Za-z]:\\Users\\)([^\\]+)", "${1}<USER>");
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void PrintHealth(ScanOutcome outcome)
    {
        var health = outcome.SourceHealth;

        Console.WriteLine();
        Console.WriteLine("关联采集源健康度：");
        Console.WriteLine("  来源                        状态      条数   说明");
        foreach (var h in health)
            Console.WriteLine($"  {h.SourceName,-26}{h.StatusText,-9}{h.ItemCount,6}   {h.ErrorMessage ?? ""}");

        // 上限由 Core 随结果一起给出，不在工具侧重算
        Console.WriteLine();
        Console.WriteLine($"完成来源：{outcome.HealthySourceCount}/{outcome.SourceHealth.Count}   引用总数：{outcome.TotalAssociationRecords}");
        Console.WriteLine($"本次置信度上限：{outcome.ConfidenceCap}"
            + (outcome.ConfidenceCapNote == null ? string.Empty : "（" + outcome.ConfidenceCapNote + "）"));
    }

    // ---------------- 参数 ----------------

    private sealed class CmdOptions
    {
        public List<string> Roots { get; init; } = new();
        public string Out { get; init; } = "";
        public bool Redact { get; init; } = true;
        public int MaxDepth { get; init; } = 2;
        public bool IncludeAll { get; init; }
        public bool FollowReparsePoints { get; init; }
        public int Limit { get; init; }
        public string SampleClass { get; init; } = "U";
        public string GitCommit { get; init; } = "unknown";
        public string? Error { get; init; }
    }

    private static CmdOptions ParseArgs(string[] args)
    {
        var roots = new List<string>();
        var outPath = System.IO.Path.Combine("validation", "raw", $"run-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        bool redact = true, includeAll = false, follow = false;
        int maxDepth = 2, limit = 0;
        string sampleClass = "U", gitCommit = "unknown";

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (a)
            {
                case "--roots":
                    var r = Next();
                    if (r == null) return new CmdOptions { Error = "--roots 缺少值" };
                    roots.AddRange(r.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--out":
                    var o = Next();
                    if (o == null) return new CmdOptions { Error = "--out 缺少值" };
                    outPath = o;
                    break;
                case "--no-redact": redact = false; break;
                case "--include-all": includeAll = true; break;
                case "--follow-reparse-points": follow = true; break;
                case "--max-depth":
                    var d = Next();
                    if (d == null || !int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDepth))
                        return new CmdOptions { Error = "--max-depth 必须是整数" };
                    break;
                case "--limit":
                    var l = Next();
                    if (l == null || !int.TryParse(l, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit))
                        return new CmdOptions { Error = "--limit 必须是整数" };
                    break;
                case "--class":
                    var c = Next();
                    if (c == null) return new CmdOptions { Error = "--class 缺少值" };
                    sampleClass = c.ToUpperInvariant();
                    break;
                case "--commit":
                    var g = Next();
                    if (g == null) return new CmdOptions { Error = "--commit 缺少值" };
                    gitCommit = g;
                    break;
                default:
                    return new CmdOptions { Error = "未知参数：" + a };
            }
        }

        if (roots.Count == 0) return new CmdOptions { Error = "必须提供 --roots" };

        return new CmdOptions
        {
            Roots = roots,
            Out = outPath,
            Redact = redact,
            MaxDepth = maxDepth,
            IncludeAll = includeAll,
            FollowReparsePoints = follow,
            Limit = limit,
            SampleClass = sampleClass,
            GitCommit = gitCommit,
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"用法：
  GhostCleaner.Validate --roots <路径;路径> [选项]

必填：
  --roots <p;p>              扫描根路径，分号分隔。例：--roots ""D:\"" 或 --roots ""D:\Games;D:\Tools""

选项：
  --out <file>               输出 JSONL 路径（默认 validation\raw\run-<时间戳>.jsonl）
  --class <A-J|U>            样本类代号前缀，默认 U（待人工归类）
  --max-depth <n>            向下探测层级，默认 2
  --include-all              评估全部候选目录（含不像软件目录的负样本，TN 统计必需）
  --follow-reparse-points    不跳过符号链接/挂载点（默认跳过）
  --limit <n>                最多输出 n 条（冒烟用）
  --no-redact                不做路径脱敏（默认脱敏）
  --commit <sha>             写入报告的 git 提交号

示例：
  GhostCleaner.Validate --roots ""D:\"" --out validation\raw\run1.jsonl --include-all
  GhostCleaner.Validate --roots ""D:\Games"" --class D --limit 20 --no-redact

安全：本工具只读。它不引用 GhostCleaner.Services，编译期即不可达 CleanupService / QuarantineManager。");
    }

    // ---------------- 输出模型（docs/core-0.1/validation/label-schema-v1.json） ----------------

    internal sealed class LabelRecord
    {
        public int SchemaVersion { get; init; } = 1;
        public string SampleId { get; init; } = "";
        public string SampleClass { get; init; } = "U";
        public string ScannedPath { get; init; } = "";
        public bool PathRedacted { get; init; }

        /// <summary>真实路径。只写入本地 path-map.local.json，不写入 JSONL。</summary>
        [JsonIgnore]
        public string RealPath { get; init; } = "";

        public string? GroundTruth { get; init; }
        public string? GroundTruthConfidence { get; init; }
        public bool? IsActuallyInUse { get; init; }
        public bool? ContainsUserData { get; init; }
        public string? HumanNote { get; init; }

        public CoreVerdict Core { get; init; } = new();
        public List<EvidenceRow> Evidence { get; init; } = new();
        public List<string> Warnings { get; init; } = new();

        /// <summary>
        /// 跨时间观测快照（v0.2 基础设施）。
        ///
        /// <para>背景：Core 0.2 已定案「只提供证据，不做自动判定」—— 单次快照无法区分
        /// 「重装后继续用」与「真废弃」（五个候选信号源全部证伪，见
        /// <c>docs/core-0.1/14-…定案.md</c>）。</para>
        ///
        /// <para>唯一可能有解的方向是 <b>跨时间观测</b>：多次扫描比对<b>趋势</b>
        /// （体积是否增长、最后写入时间是否前移），而非单次快照。
        /// 其中「最后写入时间前移」强于任何"运行过"类信号 ——
        /// 卸载动作不会让目录产生新写入。</para>
        ///
        /// <para>要能比对，先得有历史 —— 本区块即为历史服务。
        /// 从现在开始记录，未来的第一次比对才有数据可用。</para>
        /// </summary>
        public SnapshotRow Snapshot { get; init; } = new();

        /// <summary>
        /// 与 <see cref="Warnings"/> 一一对应的分类代码。
        /// 单独成列而不是把 warnings 改成对象数组，是为了不让既有分析脚本失效；
        /// 需要按类别统计时用这一列。
        /// </summary>
        public List<string> WarningCodes { get; init; } = new();
        public List<HealthRow> AssociationSourceHealth { get; init; } = new();

        /// <summary>原始环境信息（不含扫描时间），不直接序列化。</summary>
        [JsonIgnore]
        public EnvironmentRow Environment { get; init; } = new();

        [JsonIgnore] public DateTimeOffset ScannedAt { get; set; }
        [JsonIgnore] public long ScanDurationMs { get; set; }

        /// <summary>序列化用的 environment（回填扫描时间后输出）。</summary>
        [JsonPropertyName("environment")]
        public EnvironmentRow EnvironmentOut => Environment with
        {
            ScannedAt = ScannedAt,
            ScanDurationMs = ScanDurationMs,
        };
    }

    internal sealed record CoreVerdict
    {
        public int ConfidenceScore { get; init; }
        public string ConfidenceLevel { get; init; } = "";
        public int RiskScore { get; init; }
        public string Risk { get; init; } = "";
        public string Recommendation { get; init; } = "";
        public bool IsProtected { get; init; }
        public string? ProtectionReason { get; init; }
        public bool IsExcluded { get; init; }
        public string? ExcludeReason { get; init; }
        public string? DirectoryCategory { get; init; }
        public bool IsRunning { get; init; }
    }

    internal sealed record EvidenceRow
    {
        public string Type { get; init; } = "";
        public int Score { get; init; }
        public bool IsPositive { get; init; }
        public string Severity { get; init; } = "";

        /// <summary>
        /// 证据标题与描述。此前只导出 4 个结构化字段，导致"证据到底写了什么"
        /// 无法从 JSONL 查验 —— 曾因此误判过一次改动是否生效。
        /// </summary>
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
    }

    /// <summary>
    /// 跨时间观测的快照量（v0.2）。
    /// 每次扫描记录一次，用于后续比对趋势：体积增长 / 最后写入时间前移 / 文件数变化。
    /// </summary>
    internal sealed record SnapshotRow
    {
        public long SizeBytes { get; init; }
        public int FileCount { get; init; }
        public int DirectoryCount { get; init; }

        /// <summary>
        /// 目录最后写入时间。用 ISO 8601 往返格式（"o"），
        /// 避免跨机器比较时的时区与文化差异问题。
        /// </summary>
        public string LastModified { get; init; } = "";

        /// <summary>
        /// 主程序完整路径。
        /// 此前 JSONL 未导出它，导致 Prefetch 匹配脚本取到空值（41/41 假阴性）。
        /// </summary>
        public List<string> MainExecutables { get; init; } = new();
    }

    internal sealed record HealthRow
    {
        public string SourceName { get; init; } = "";
        public string Status { get; init; } = "";
        public int ItemCount { get; init; }
        public string? ErrorMessage { get; init; }
    }

    internal sealed record EnvironmentRow
    {
        public string? OsVersion { get; init; }
        public string CoreVersion { get; init; } = "0.1";
        public string GitCommit { get; init; } = "unknown";
        public bool? RunAsAdmin { get; init; }
        public DateTimeOffset? ScannedAt { get; init; }
        public long? ScanDurationMs { get; init; }
    }
}

using WinCleaner.Core.Interfaces;
using WinCleaner.Core.Models;
using WinCleaner.Core.Utils;

namespace WinCleaner.Scanner;

/// <summary>对单个目录做文件系统层面的度量与"是否像软件安装目录"打分。</summary>
public sealed class SoftwareDirectoryInspector : ISoftwareDirectoryInspector
{
    public Task<SoftwareDirectoryInfo?> InspectAsync(string directoryPath, ScanOptions options, CancellationToken cancellationToken = default)
        => Task.Run(() => Inspect(directoryPath, options, cancellationToken), cancellationToken);

    private static SoftwareDirectoryInfo? Inspect(string directoryPath, ScanOptions options, CancellationToken ct)
    {
        var dir = PathUtils.Normalize(directoryPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

        var info = new SoftwareDirectoryInfo { Path = dir, Name = System.IO.Path.GetFileName(dir) };

        DirectoryInfo rootInfo;
        try
        {
            rootInfo = new DirectoryInfo(dir);
            info.Created = rootInfo.CreationTime;
            info.LastModified = rootInfo.LastWriteTime;
            if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0) info.HasReparsePoint = true;
        }
        catch
        {
            return null;
        }

        int fileCount = 0, dllCount = 0, mediaCount = 0, archiveCount = 0, userDataFileCount = 0;
        long totalSize = 0;
        var lastWrite = info.LastModified;
        var executables = new List<(string Path, long Size)>();
        bool truncated = false;

        var fileOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = options.IncludeHiddenDirectories
                ? FileAttributes.System
                : FileAttributes.Hidden | FileAttributes.System,
        };

        try
        {
            foreach (var file in rootInfo.EnumerateFiles("*", fileOptions))
            {
                ct.ThrowIfCancellationRequested();

                if (fileCount >= options.MaxFileCountPerDirectory)
                {
                    truncated = true;
                    break;
                }

                long length;
                DateTime writeTime;
                try
                {
                    length = file.Length;
                    writeTime = file.LastWriteTime;
                }
                catch
                {
                    continue;
                }

                fileCount++;
                totalSize += length;
                if (writeTime > lastWrite) lastWrite = writeTime;

                var ext = file.Extension.ToLowerInvariant();
                if (ext == ".exe") executables.Add((file.FullName, length));
                else if (ext is ".dll" or ".node") dllCount++;

                if (Heuristics.IsMediaFile(ext)) mediaCount++;
                if (Heuristics.IsArchiveFile(ext)) archiveCount++;
                if (Heuristics.IsUserDataFile(ext))
                {
                    userDataFileCount++;
                    if (info.UserDataSamples.Count < 5) info.UserDataSamples.Add(file.Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 部分文件不可访问，按已统计结果继续
        }

        // 子目录特征
        try
        {
            foreach (var sub in rootInfo.EnumerateDirectories("*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = options.IncludeHiddenDirectories ? 0 : FileAttributes.Hidden,
            }))
            {
                ct.ThrowIfCancellationRequested();
                var name = sub.Name.ToLowerInvariant();

                if (Heuristics.IsResourceDirectory(name) && !info.ResourceDirectories.Contains(sub.Name))
                    info.ResourceDirectories.Add(sub.Name);

                if (Heuristics.IsUserDataDirectory(name))
                {
                    info.HasUserData = true;
                    if (info.UserDataSamples.Count < 10) info.UserDataSamples.Add(sub.Name + "\\");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 忽略
        }

        info.FileCount = fileCount;
        info.TotalSizeBytes = totalSize;
        info.LibraryCount = dllCount;
        info.ExecutableCount = executables.Count;
        info.LastModified = lastWrite;
        info.ExecutablePaths.AddRange(executables.OrderByDescending(x => x.Size).Take(5).Select(x => x.Path));

        if (userDataFileCount > 0) info.HasUserData = true;

        var uninstaller = executables.FirstOrDefault(x => Heuristics.IsUninstaller(x.Path));
        if (uninstaller.Path != null)
        {
            info.HasUninstaller = true;
            info.UninstallerPath = uninstaller.Path;
        }

        var version = VersionInfoReader.ReadAny(executables.OrderByDescending(x => x.Size).Take(3).Select(x => x.Path));
        if (version is { HasAny: true })
        {
            info.HasVersionResource = true;
            info.Version = version.Version;
            info.Publisher = version.Publisher;
            info.ProductName = version.ProductName;
        }

        Score(info, executables, mediaCount, archiveCount, options, truncated);

        return info;
    }

    private static void Score(
        SoftwareDirectoryInfo info,
        List<(string Path, long Size)> executables,
        int mediaCount,
        int archiveCount,
        ScanOptions options,
        bool truncated)
    {
        int score = 0;

        if (info.ExecutableCount >= 1)
        {
            score += 30;
            info.ScoreDetails.Add($"包含 {info.ExecutableCount} 个可执行文件");
        }
        if (info.ExecutableCount >= 3) score += 10;
        if (info.ExecutableCount >= 10) score += 5;

        if (info.HasUninstaller)
        {
            score += 25;
            info.ScoreDetails.Add("存在卸载程序（" + System.IO.Path.GetFileName(info.UninstallerPath) + "）");
        }

        if (info.LibraryCount >= 1)
        {
            score += 12;
            info.ScoreDetails.Add($"包含 {info.LibraryCount} 个动态库");
        }
        if (info.LibraryCount >= 10) score += 8;

        if (info.HasVersionResource)
        {
            score += 10;
            info.ScoreDetails.Add("读取到版本/厂商信息");
        }
        if (!string.IsNullOrEmpty(info.Publisher))
        {
            score += 8;
            info.ScoreDetails.Add("厂商：" + info.Publisher);
        }
        if (!string.IsNullOrEmpty(info.ProductName))
        {
            score += 5;
            info.ScoreDetails.Add("产品名：" + info.ProductName);
        }
        if (info.ResourceDirectories.Count > 0)
        {
            score += 10;
            info.ScoreDetails.Add("存在资源目录：" + string.Join("、", info.ResourceDirectories.Take(3)));
        }
        if (NameMatchesExecutable(info, executables))
        {
            score += 10;
            info.ScoreDetails.Add("主程序名与目录名一致");
        }
        if (info.FileCount >= 20) score += 8;
        if (info.FileCount >= 200) score += 7;
        if (info.TotalSizeBytes >= 10L * 1024 * 1024) score += 8;
        if (info.TotalSizeBytes >= 200L * 1024 * 1024) score += 7;

        // —— 反向证据（减分）——
        if (info.FileCount > 0 && (double)mediaCount / info.FileCount > 0.7)
        {
            score -= 30;
            info.ScoreDetails.Add("媒体文件占比过高，更像媒体库而非软件目录");
        }
        if (info.FileCount > 0 && (double)archiveCount / info.FileCount > 0.5)
        {
            score -= 15;
            info.ScoreDetails.Add("安装包/压缩包占多数，更像下载目录");
        }
        if (info.ExecutableCount == 1 && info.LibraryCount == 0 && info.TotalSizeBytes < 1024 * 1024)
        {
            score -= 10;
            info.ScoreDetails.Add("仅有一个很小的 exe，特征较弱");
        }
        if (truncated) info.ScoreDetails.Add("文件数量超过统计上限，统计可能不完整");

        info.SoftwareScore = Math.Clamp(score, 0, 100);

        // 必须至少包含一个可执行文件 —— 没有 exe 的目录绝不认为是软件安装目录
        info.IsLikelySoftwareDirectory =
            info.ExecutableCount >= 1 &&
            info.SoftwareScore >= options.MinSoftwareScore;
    }

    private static bool NameMatchesExecutable(SoftwareDirectoryInfo info, List<(string Path, long Size)> executables)
    {
        if (executables.Count == 0) return false;

        static string Normalize(string s)
        {
            var chars = s.ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch))
                .ToArray();
            return new string(chars);
        }

        var dirName = Normalize(info.Name);
        if (dirName.Length == 0) return false;

        return executables.Any(x => Normalize(System.IO.Path.GetFileNameWithoutExtension(x.Path)) == dirName);
    }
}

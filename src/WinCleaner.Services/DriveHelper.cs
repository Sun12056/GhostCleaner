namespace WinCleaner.Services;

/// <summary>磁盘相关辅助：枚举固定磁盘、推荐隔离区位置。</summary>
public static class DriveHelper
{
    /// <summary>系统盘根（例如 C:\）。</summary>
    public static string SystemDriveRoot =>
        System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";

    public static bool IsSystemDrive(string? driveRoot)
        => !string.IsNullOrEmpty(driveRoot) &&
           driveRoot.Equals(SystemDriveRoot, StringComparison.OrdinalIgnoreCase);

    /// <summary>列出可用的固定磁盘（排除系统盘，优先剩余空间大的）。</summary>
    public static List<DriveInfo> GetNonSystemFixedDrives()
    {
        var systemRoot = SystemDriveRoot;
        var list = new List<DriveInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                if (drive.RootDirectory.FullName.Equals(systemRoot, StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(drive);
            }
            catch
            {
                // 忽略不可读的盘
            }
        }

        return list.OrderByDescending(d => d.AvailableFreeSpace).ToList();
    }

    /// <summary>列出所有就绪的固定磁盘（含系统盘，仅用于展示）。</summary>
    public static List<DriveInfo> GetAllFixedDrives()
    {
        var list = new List<DriveInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady) list.Add(drive);
            }
            catch
            {
                // 忽略
            }
        }
        return list;
    }

    /// <summary>推荐隔离区路径：优先非系统盘剩余空间最大的盘。</summary>
    public static string SuggestQuarantineRoot()
    {
        var nonSystem = GetNonSystemFixedDrives();
        if (nonSystem.Count > 0)
            return System.IO.Path.Combine(nonSystem[0].RootDirectory.FullName, "CleanerQuarantine");

        return System.IO.Path.Combine(SystemDriveRoot, "CleanerQuarantine");
    }
}

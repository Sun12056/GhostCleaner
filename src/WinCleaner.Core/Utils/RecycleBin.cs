namespace WinCleaner.Core.Utils;

/// <summary>回收站操作封装。</summary>
public static class RecycleBin
{
    /// <summary>删除到回收站；失败返回 false（调用方需自行处理，不做静默永久删除）。</summary>
    public static bool Send(string path)
    {
        if (!System.IO.Path.Exists(path)) return false;

        try
        {
            return NativeMethods.DeleteToRecycleBin(path);
        }
        catch
        {
            return false;
        }
    }
}

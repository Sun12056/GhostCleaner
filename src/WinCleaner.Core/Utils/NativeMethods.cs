using System.Runtime.InteropServices;

namespace WinCleaner.Core.Utils;

/// <summary>Shell32 文件操作：删除到回收站。</summary>
internal static class NativeMethods
{
    private const uint FoDelete = 3;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofNoConfirmMkDir = 0x0200;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    /// <summary>把文件或目录删除到回收站。pFrom 需要双 \0 结尾。</summary>
    public static bool DeleteToRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var op = new SHFILEOPSTRUCT
        {
            wFunc = FoDelete,
            pFrom = PathUtils.Normalize(path) + "\0\0",
            fFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi | FofNoConfirmMkDir,
        };

        int result = SHFileOperation(ref op);
        return result == 0 && !Convert.ToBoolean(op.fAnyOperationsAborted);
    }
}

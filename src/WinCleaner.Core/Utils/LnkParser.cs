using System.Text;

namespace WinCleaner.Core.Utils;

/// <summary>极简 .lnk 解析（不依赖 COM），用于提取快捷方式指向的目标路径。</summary>
public static class LnkParser
{
    private const int HeaderSize = 0x4C;

    public static string? GetTargetPath(string lnkPath)
    {
        try
        {
            using var fs = new FileStream(lnkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            var header = br.ReadBytes(HeaderSize);
            if (header.Length < HeaderSize) return null;
            if (BitConverter.ToUInt32(header, 0x00) != HeaderSize) return null;

            uint linkFlags = BitConverter.ToUInt32(header, 0x14);
            bool hasIdList = (linkFlags & 0x0000_0001) != 0;
            bool hasLinkInfo = (linkFlags & 0x0000_0002) != 0;
            bool hasName = (linkFlags & 0x0000_0004) != 0;
            bool hasRelativePath = (linkFlags & 0x0000_0008) != 0;
            bool hasWorkingDir = (linkFlags & 0x0000_0010) != 0;
            bool hasArguments = (linkFlags & 0x0000_0020) != 0;
            bool hasIconLocation = (linkFlags & 0x0000_0040) != 0;
            bool isUnicode = (linkFlags & 0x0000_0080) != 0;

            long offset = HeaderSize;

            if (hasIdList)
            {
                fs.Position = offset;
                ushort idListSize = br.ReadUInt16();
                offset += 2 + idListSize;
            }

            if (hasLinkInfo)
            {
                fs.Position = offset;
                uint linkInfoSize = br.ReadUInt32();
                offset += linkInfoSize;
            }

            string? name = hasName ? ReadString(fs, br, isUnicode, ref offset) : null;
            string? relativePath = hasRelativePath ? ReadString(fs, br, isUnicode, ref offset) : null;
            string? workingDir = hasWorkingDir ? ReadString(fs, br, isUnicode, ref offset) : null;
            _ = hasArguments ? ReadString(fs, br, isUnicode, ref offset) : null;
            _ = hasIconLocation ? ReadString(fs, br, isUnicode, ref offset) : null;

            var candidate = relativePath ?? name;
            if (string.IsNullOrWhiteSpace(candidate)) return null;

            if (System.IO.Path.IsPathRooted(candidate)) return PathUtils.Normalize(candidate);

            // 相对路径：优先基于 lnk 所在目录解析，其次基于工作目录
            var baseDir = System.IO.Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(workingDir) && System.IO.Path.IsPathRooted(workingDir))
                baseDir = workingDir;
            if (string.IsNullOrEmpty(baseDir)) return null;

            return PathUtils.Normalize(System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, candidate)));
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadString(FileStream fs, BinaryReader br, bool isUnicode, ref long offset)
    {
        fs.Position = offset;
        ushort count = br.ReadUInt16();
        int byteCount = isUnicode ? count * 2 : count;
        var bytes = br.ReadBytes(byteCount);
        offset += 2 + byteCount;
        return isUnicode ? Encoding.Unicode.GetString(bytes) : Encoding.Default.GetString(bytes);
    }
}

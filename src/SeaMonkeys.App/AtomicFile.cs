using System.Text;

namespace SeaMonkeys.App;

/// <summary>用户数据文件的原子读写:先写临时文件再替换,避免半截文件;</summary>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = path + ".tmp";
        File.WriteAllText(tmp, contents, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>把现有文件复制为 .bak,用于解析失败后的人工恢复。</summary>
    public static void Backup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
        }
        catch
        {
        }
    }
}

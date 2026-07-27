using System.IO;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class LockfileService
{
    public LockfileInfo Read(string lockfilePath)
    {
        if (!File.Exists(lockfilePath))
            throw new FileNotFoundException(
                "League Client lockfile bulunamadı.",
                lockfilePath
            );

        string content;

        using (var stream = new FileStream(
                   lockfilePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            content = reader.ReadToEnd();
        }

        string[] parts = content.Trim().Split(':');

        if (parts.Length != 5)
            throw new InvalidOperationException(
                "League Client lockfile formatı geçersiz."
            );

        return new LockfileInfo
        {
            ProcessName = parts[0],
            ProcessId = int.Parse(parts[1]),
            Port = int.Parse(parts[2]),
            Password = parts[3],
            Protocol = parts[4]
        };
    }
}
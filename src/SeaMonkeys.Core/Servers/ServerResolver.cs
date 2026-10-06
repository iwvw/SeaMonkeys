using SeaMonkeys.Core.Models;

namespace SeaMonkeys.Core.Servers;

public sealed class ServerResolver
{
    public const string AutoDetectionFailed = "ServerAutoDetectionFailed";

    public Server Resolve(Server configured, string gamePath)
    {
        if (configured != Server.Auto)
        {
            return configured;
        }

        string logPath = Path.Combine(gamePath, "profile", "clientrunner.log");
        return DetectFromClientRunnerLog(logPath);
    }

    public static Server DetectFromClientRunnerLog(string logPath)
    {
        try
        {
            using FileStream stream = new(
                logPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string content = reader.ReadToEnd();

            const string marker = "Selected realm: ";
            int index = content.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException("realm marker not found");
            }

            int start = index + marker.Length;
            int end = content.IndexOf('\n', start);
            string realm = end < 0 ? content[start..] : content[start..end];

            if (ServerExtensions.TryParseCode(realm, out Server server))
            {
                return server;
            }

            throw new InvalidOperationException($"unknown realm '{realm.Trim()}'");
        }
        catch (Exception ex)
        {
            throw new ServerResolutionException(AutoDetectionFailed, ex);
        }
    }
}

public sealed class ServerResolutionException : Exception
{
    public ServerResolutionException(string message, Exception inner) : base(message, inner)
    {
    }

    public ServerResolutionException(string message) : base(message)
    {
    }
}

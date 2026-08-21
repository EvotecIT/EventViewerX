using System.Security.Cryptography;

namespace EventViewerX.Providers;

internal static class EventProviderHash {
    internal static string FileSha256(string path) {
        using FileStream stream = File.OpenRead(path);
        return StreamSha256(stream);
    }

    internal static string StreamSha256(Stream stream) {
        using SHA256 sha256 = SHA256.Create();
        return ToHex(sha256.ComputeHash(stream));
    }

    internal static string BytesSha256(byte[] bytes) {
        using SHA256 sha256 = SHA256.Create();
        return ToHex(sha256.ComputeHash(bytes));
    }

    private static string ToHex(byte[] bytes) {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) {
            builder.Append(value.ToString("x2"));
        }
        return builder.ToString();
    }
}

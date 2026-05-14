using System.Text;
using System.Text.Json;

namespace Tsz.Infrastructure.Common.Pagination;

public sealed record KeysetCursor(int V, JsonElement SortValue, Guid Id)
{
    private const int CurrentVersion = 1;

    public string Encode()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(this);
        return Base64UrlEncode(json);
    }

    public static bool TryDecode(string? raw, out KeysetCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            var bytes = Base64UrlDecode(raw);
            var decoded = JsonSerializer.Deserialize<KeysetCursor>(bytes);
            if (decoded is null || decoded.V != CurrentVersion)
                return false;
            cursor = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}

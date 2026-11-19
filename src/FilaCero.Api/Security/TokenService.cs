using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FilaCero.Api.Security;

/// <summary>Tokens de 2 horas; el secreto sólo vive en el backend.</summary>
public sealed class TokenService(string secret)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/') + new string('=', (4 - s.Length % 4) % 4));

    private string Sign(string value) => Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));
    private static bool Same(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public string Issue(Guid patientId)
    {
        var content = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new SessionPayload(patientId, DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()), Json));
        return $"{content}.{Sign(content)}";
    }

    public Guid? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 2 || !Same(Sign(parts[0]), parts[1])) return null;
        try
        {
            var payload = JsonSerializer.Deserialize<SessionPayload>(FromBase64Url(parts[0]), Json);
            return payload is not null && payload.Exp > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() && payload.Sub != Guid.Empty ? payload.Sub : null;
        }
        catch (Exception e) when (e is FormatException or JsonException) { return null; }
    }

    private sealed record SessionPayload(Guid Sub, long Exp);

    public string NewNonce() => Base64Url(RandomNumberGenerator.GetBytes(9));
    public string QrPayload(string code, string nonce)
    {
        var raw = $"FC1|{code}|{nonce}";
        return $"{raw}|{Sign(raw)[..22]}";
    }

    public bool VerifyQr(string? payload)
    {
        if (payload is null || !Regex.IsMatch(payload, @"^FC1\|A-\d{3,6}\|[A-Za-z0-9_-]{12,32}\|[A-Za-z0-9_-]{22}$")) return false;
        var last = payload.LastIndexOf('|');
        return Same(Sign(payload[..last])[..22], payload[(last + 1)..]);
    }
}

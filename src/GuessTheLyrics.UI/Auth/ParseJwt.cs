using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GuessTheLyrics.UI.Auth;

/// <summary>Reads the claims from a JWT payload. No signature check: the API does that on every request.</summary>
public static class ParseJwt
{
    public static List<Claim> GetClaims(string token)
    {
        var claims = new List<Claim>();
        var parts = token.Split('.');
        if (parts.Length != 3)
            return claims;

        using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                    claims.Add(new Claim(property.Name, item.ToString()));
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return claims;
    }

    public static bool IsExpired(IEnumerable<Claim> claims)
    {
        var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        return !long.TryParse(exp, out var seconds)
               || DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };
        return Convert.FromBase64String(base64);
    }
}

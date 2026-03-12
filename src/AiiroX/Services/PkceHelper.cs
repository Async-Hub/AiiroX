using System;
using System.Security.Cryptography;
using System.Text;

namespace AiiroX.Services;

/// <summary>
/// Utilities for generating PKCE (Proof Key for Code Exchange) parameters per RFC 7636.
/// </summary>
internal static class PkceHelper
{
    /// <summary>
    /// Generates a cryptographically random code verifier.
    /// 32 bytes of entropy, base64url-encoded (no padding) → 43-character string.
    /// </summary>
    public static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Computes the S256 code challenge for the given <paramref name="codeVerifier"/>:
    /// <c>BASE64URL(SHA256(ASCII(codeVerifier)))</c>.
    /// </summary>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Encodes <paramref name="data"/> as a base64url string (no padding, URL-safe alphabet).
    /// </summary>
    public static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

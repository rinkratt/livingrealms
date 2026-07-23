using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace LivingRealms.Api.Security;

public static class SessionToken
{
    public static string Create()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }
}

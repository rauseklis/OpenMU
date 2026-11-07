// <copyright file="RequestSigner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Security;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Computes and checks the request signature of the website API:
/// lowercase hex of <c>HMAC-SHA256(secret, METHOD LF PATH?QUERY LF TIMESTAMP LF SHA256hex(body))</c>.
/// </summary>
public static class RequestSigner
{
    /// <summary>The allowed difference between the timestamp of a request and the server clock, in seconds.</summary>
    public const int AllowedClockSkewSeconds = 60;

    /// <summary>
    /// Computes the signature.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path with the query string, exactly as sent on the wire.</param>
    /// <param name="timestamp">The unix timestamp in seconds, as sent.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The signature as lowercase hex.</returns>
    public static string Sign(string secret, string method, string pathAndQuery, string timestamp, ReadOnlySpan<byte> body)
    {
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var canonical = $"{method.ToUpperInvariant()}\n{pathAndQuery}\n{timestamp}\n{bodyHash}";
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Compares two signatures in constant time.
    /// </summary>
    /// <param name="expected">The expected signature.</param>
    /// <param name="actual">The signature of the request.</param>
    /// <returns><c>true</c> if they are equal.</returns>
    public static bool AreEqual(string expected, string actual)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual.ToLowerInvariant()));
    }

    /// <summary>
    /// Checks whether the timestamp is within the allowed window.
    /// </summary>
    /// <param name="timestamp">The timestamp text.</param>
    /// <param name="now">The current time.</param>
    /// <returns><c>true</c> if the timestamp is a unix time within the window.</returns>
    public static bool IsFresh(string timestamp, DateTimeOffset now)
    {
        return long.TryParse(timestamp, out var seconds)
            && Math.Abs(now.ToUnixTimeSeconds() - seconds) <= AllowedClockSkewSeconds;
    }
}

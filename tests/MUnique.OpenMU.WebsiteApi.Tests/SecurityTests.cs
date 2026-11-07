// <copyright file="SecurityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using System.Security.Cryptography;
using System.Text;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// Tests the signature, replay protection and rate limiting building blocks.
/// </summary>
[TestFixture]
public class SecurityTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The signature is the HMAC-SHA256 of "METHOD LF PATH?QUERY LF TIMESTAMP LF SHA256hex(body)" as lowercase hex,
    /// computed here independently from the implementation.
    /// </summary>
    [Test]
    public void SignatureMatchesTheContract()
    {
        var body = Encoding.UTF8.GetBytes("{\"loginName\":\"abc\"}");
        var bodyHash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var canonical = "POST\n/api/v1/auth/verify?x=1&b=2\n1700000000\n" + bodyHash;
        var expected = Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(Secret)).ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        var actual = RequestSigner.Sign(Secret, "POST", "/api/v1/auth/verify?x=1&b=2", "1700000000", body);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Does.Match("^[0-9a-f]{64}$"));
    }

    /// <summary>
    /// An empty body hashes to the SHA-256 of the empty string.
    /// </summary>
    [Test]
    public void EmptyBodyUsesTheHashOfTheEmptyString()
    {
        var canonical = "GET\n/api/v1/status\n1700000000\ne3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var expected = Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(Secret)).ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        Assert.That(RequestSigner.Sign(Secret, "GET", "/api/v1/status", "1700000000", []), Is.EqualTo(expected));
    }

    /// <summary>
    /// A different query order or method gives a different signature: the query is signed exactly as sent.
    /// </summary>
    [Test]
    public void QueryAndMethodAreSigned()
    {
        var a = RequestSigner.Sign(Secret, "GET", "/p?a=1&b=2", "1", []);
        Assert.That(RequestSigner.Sign(Secret, "GET", "/p?b=2&a=1", "1", []), Is.Not.EqualTo(a));
        Assert.That(RequestSigner.Sign(Secret, "POST", "/p?a=1&b=2", "1", []), Is.Not.EqualTo(a));
    }

    /// <summary>
    /// Timestamps are accepted within 60 seconds in both directions, and only as numbers.
    /// </summary>
    [Test]
    public void TimestampWindowIs60Seconds()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        Assert.That(RequestSigner.IsFresh("1700000000", now), Is.True);
        Assert.That(RequestSigner.IsFresh("1699999940", now), Is.True);
        Assert.That(RequestSigner.IsFresh("1700000060", now), Is.True);
        Assert.That(RequestSigner.IsFresh("1699999939", now), Is.False);
        Assert.That(RequestSigner.IsFresh("1700000061", now), Is.False);
        Assert.That(RequestSigner.IsFresh("yesterday", now), Is.False);
        Assert.That(RequestSigner.IsFresh(string.Empty, now), Is.False);
    }

    /// <summary>
    /// Signatures are compared without regard to the case of the hex digits, but not otherwise.
    /// </summary>
    [Test]
    public void SignatureComparison()
    {
        var signature = RequestSigner.Sign(Secret, "GET", "/x", "1", []);
        Assert.That(RequestSigner.AreEqual(signature, signature.ToUpperInvariant()), Is.True);
        Assert.That(RequestSigner.AreEqual(signature, signature[..^1] + (signature[^1] == '0' ? "1" : "0")), Is.False);
        Assert.That(RequestSigner.AreEqual(signature, "abc"), Is.False);
    }

    /// <summary>
    /// A signature is accepted once within the window.
    /// </summary>
    [Test]
    public void ReplayCacheRefusesTheSameSignatureTwice()
    {
        var cache = new ReplayCache();
        var now = DateTimeOffset.UtcNow;
        Assert.That(cache.TryRegister("abc", now), Is.True);
        Assert.That(cache.TryRegister("abc", now.AddSeconds(5)), Is.False);
        Assert.That(cache.TryRegister("ABC", now.AddSeconds(5)), Is.False);
        Assert.That(cache.TryRegister("def", now), Is.True);

        // After the window the timestamp check refuses the request anyway, and the entry may be used again.
        Assert.That(cache.TryRegister("abc", now.AddSeconds(200)), Is.True);
    }

    /// <summary>
    /// The limiter lets the limit through, refuses the next hit and recovers when the window has passed.
    /// </summary>
    [Test]
    public void RateLimiterSlidesAndResets()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new RateLimiter(() => now);
        var window = TimeSpan.FromMinutes(1);

        Assert.That(limiter.TryHit("k", 3, window), Is.True);
        Assert.That(limiter.TryHit("k", 3, window), Is.True);
        Assert.That(limiter.TryHit("k", 3, window), Is.True);
        Assert.That(limiter.TryHit("k", 3, window), Is.False);
        Assert.That(limiter.TryHit("other", 3, window), Is.True, "keys are independent");

        now = now.AddSeconds(61);
        Assert.That(limiter.TryHit("k", 3, window), Is.True);

        limiter.Reset("k");
        Assert.That(limiter.TryHit("k", 1, window), Is.True);
    }
}

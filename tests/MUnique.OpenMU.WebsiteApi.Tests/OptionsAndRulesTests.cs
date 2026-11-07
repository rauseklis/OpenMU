// <copyright file="OptionsAndRulesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using MUnique.OpenMU.WebsiteApi.Accounts;
using MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// Tests the environment configuration, the account rules and the guild logo conversion.
/// </summary>
[TestFixture]
public class OptionsAndRulesTests
{
    private static readonly string Key = "site:" + new string('k', 40);

    /// <summary>
    /// Without a port the API is off, silently.
    /// </summary>
    [Test]
    public void IsOffWithoutPort()
    {
        Assert.That(Parse(), Is.Null);
        Assert.That(Parse((WebsiteApiOptions.KeysVariableName, Key)), Is.Null);
        Assert.That(Parse((WebsiteApiOptions.PortVariableName, " ")), Is.Null);
    }

    /// <summary>
    /// A configured API without keys, with a short secret or without TLS does not start.
    /// </summary>
    [Test]
    public void RefusesUnsafeConfigurations()
    {
        Parse(out var error, (WebsiteApiOptions.PortVariableName, "8443"));
        Assert.That(error, Does.Contain(WebsiteApiOptions.KeysVariableName));

        Parse(out error, (WebsiteApiOptions.PortVariableName, "8443"), (WebsiteApiOptions.KeysVariableName, "site:short"), (WebsiteApiOptions.CertificatePathVariableName, "a.pfx"));
        Assert.That(error, Does.Contain("shorter"));

        Parse(out error, (WebsiteApiOptions.PortVariableName, "8443"), (WebsiteApiOptions.KeysVariableName, Key));
        Assert.That(error, Does.Contain("TLS"));

        Parse(out error, (WebsiteApiOptions.PortVariableName, "99999"), (WebsiteApiOptions.KeysVariableName, Key));
        Assert.That(error, Does.Contain("port"));

        Parse(out error, (WebsiteApiOptions.PortVariableName, "8443"), (WebsiteApiOptions.KeysVariableName, "nocolon"), (WebsiteApiOptions.CertificatePathVariableName, "a.pfx"));
        Assert.That(error, Is.Not.Null);
    }

    /// <summary>
    /// A complete configuration is read, including several keys and the allow list.
    /// </summary>
    [Test]
    public void ParsesACompleteConfiguration()
    {
        var options = Parse(
            out var error,
            (WebsiteApiOptions.PortVariableName, "8443"),
            (WebsiteApiOptions.KeysVariableName, Key + ",other:" + new string('z', 32)),
            (WebsiteApiOptions.CertificatePathVariableName, "/certs/api.pfx"),
            (WebsiteApiOptions.AllowedIpsVariableName, "203.0.113.7, 198.51.100.2"),
            (WebsiteApiOptions.ReadConnectionVariableName, "Host=x"));

        Assert.That(error, Is.Null);
        Assert.That(options!.EndPoint.Port, Is.EqualTo(8443));
        Assert.That(options.Keys.Keys, Is.EquivalentTo(new[] { "site", "other" }));
        Assert.That(options.CertificatePath, Is.EqualTo("/certs/api.pfx"));
        Assert.That(options.AllowedIps, Has.Count.EqualTo(2));
        Assert.That(options.ReadConnectionString, Is.EqualTo("Host=x"));
    }

    /// <summary>
    /// Plain HTTP only with the explicit switch.
    /// </summary>
    [Test]
    public void PlainHttpNeedsTheSwitch()
    {
        var options = Parse(out var error, (WebsiteApiOptions.PortVariableName, "8443"), (WebsiteApiOptions.KeysVariableName, Key), (WebsiteApiOptions.AllowHttpVariableName, "true"));
        Assert.That(error, Is.Null);
        Assert.That(options!.CertificatePath, Is.Null);
    }

    /// <summary>
    /// Login names: 3 to 10 letters or digits.
    /// </summary>
    [TestCase("abc", true)]
    [TestCase("Player1234", true)]
    [TestCase("ab", false)]
    [TestCase("player12345", false)]
    [TestCase("pl ayer", false)]
    [TestCase("pla-yer", false)]
    [TestCase("játékos", false)]
    [TestCase("abc\n", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void LoginNameRules(string? name, bool valid) => Assert.That(AccountRules.IsValidLoginName(name), Is.EqualTo(valid));

    /// <summary>
    /// Passwords: 8 to 20 printable ASCII characters without spaces.
    /// </summary>
    [TestCase("12345678", true)]
    [TestCase("abcdefgh12345678!@#$", true)]
    [TestCase("1234567", false)]
    [TestCase("abcdefgh12345678!@#$%", false)]
    [TestCase("pass word1", false)]
    [TestCase("pässword12", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void PasswordRules(string? password, bool valid) => Assert.That(AccountRules.IsValidPassword(password), Is.EqualTo(valid));

    /// <summary>
    /// E-mail addresses need a user, a host with a dot and no decoration.
    /// </summary>
    [TestCase("a@example.com", true)]
    [TestCase("first.last+tag@mail.example.org", true)]
    [TestCase("no-at.example.com", false)]
    [TestCase("a@localhost", false)]
    [TestCase("Name <a@example.com>", false)]
    [TestCase(" a@example.com", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void EmailRules(string? email, bool valid) => Assert.That(AccountRules.IsValidEmail(email), Is.EqualTo(valid));

    /// <summary>
    /// A banned, temporarily banned, template or bot account may not log in on the website.
    /// </summary>
    [Test]
    public void OnlyUsableAccountsCanLogIn()
    {
        AccountRecord Record(MUnique.OpenMU.DataModel.Entities.AccountState state, bool template = false, bool bot = false) => new(Guid.NewGuid(), "x", "h", state, template, bot);

        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.Normal).CanLogIn, Is.True);
        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.GameMaster).CanLogIn, Is.True);
        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.Banned).CanLogIn, Is.False);
        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.TemporarilyBanned).CanLogIn, Is.False);
        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.Normal, template: true).CanLogIn, Is.False);
        Assert.That(Record(MUnique.OpenMU.DataModel.Entities.AccountState.Normal, bot: true).CanLogIn, Is.False);
    }

    /// <summary>
    /// The guild logo becomes a 64x64 RGBA PNG; the first pixel (color 3, white) is opaque white, color 0 is transparent.
    /// </summary>
    [Test]
    public void GuildLogoBecomesAPng()
    {
        var logo = new byte[GuildLogoPng.LogoSize];
        logo[0] = 0x30; // pixel 0 = color 3 (high nibble), pixel 1 = color 0 (low nibble)
        var png = GuildLogoPng.Create(logo)!;

        Assert.That(png.Take(8), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), Is.EqualTo(64));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)), Is.EqualTo(64));

        // Inflate the IDAT chunk: after the filter byte come the RGBA pixels of the first row.
        var idatLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
        using var inflated = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(new MemoryStream(png, 41, idatLength), System.IO.Compression.CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        Assert.That(raw, Has.Length.EqualTo(64 * ((64 * 4) + 1)));
        Assert.That(raw.Skip(1).Take(4), Is.EqualTo(new byte[] { 255, 255, 255, 255 }), "pixel 0 is white");
        Assert.That(raw.Skip(1 + (8 * 4)).Take(4), Is.EqualTo(new byte[] { 0, 0, 0, 0 }), "pixel 1 is transparent");
    }

    /// <summary>
    /// A missing or wrongly sized logo gives no image.
    /// </summary>
    [Test]
    public void MissingLogoGivesNothing()
    {
        Assert.That(GuildLogoPng.Create(null), Is.Null);
        Assert.That(GuildLogoPng.Create(new byte[10]), Is.Null);
    }

    private static WebsiteApiOptions? Parse(params (string Name, string Value)[] variables) => Parse(out _, variables);

    private static WebsiteApiOptions? Parse(out string? error, params (string Name, string Value)[] variables)
    {
        var map = variables.ToDictionary(v => v.Name, v => v.Value);
        return WebsiteApiOptions.FromEnvironment(name => map.GetValueOrDefault(name), out error);
    }
}

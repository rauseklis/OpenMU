// <copyright file="WebsiteApiOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi;

using System.Net;
using System.Text;

/// <summary>
/// The settings of the website API. They come from environment variables only (never from a repository file),
/// and the API stays off unless <see cref="PortVariableName"/> and <see cref="KeysVariableName"/> are both set.
/// </summary>
public sealed class WebsiteApiOptions
{
    /// <summary>The variable which enables the API by naming its port.</summary>
    public const string PortVariableName = "OPENMU_WEBSITE_API_PORT";

    /// <summary>The variable with the address to bind; unset, all interfaces (the firewall decides who may connect).</summary>
    public const string AddressVariableName = "OPENMU_WEBSITE_API_ADDRESS";

    /// <summary>The variable with the API keys, <c>keyId:secret</c>, several separated by commas.</summary>
    public const string KeysVariableName = "OPENMU_WEBSITE_API_KEYS";

    /// <summary>The variable with the path of the PFX/PKCS#12 certificate for TLS.</summary>
    public const string CertificatePathVariableName = "OPENMU_WEBSITE_API_CERT_PATH";

    /// <summary>The variable with the password of the certificate file (optional).</summary>
    public const string CertificatePasswordVariableName = "OPENMU_WEBSITE_API_CERT_PASSWORD";

    /// <summary>The variable which allows plain HTTP (without a certificate) when set to <c>true</c>; for tests and local use only.</summary>
    public const string AllowHttpVariableName = "OPENMU_WEBSITE_API_ALLOW_HTTP";

    /// <summary>The variable with an optional comma separated list of the IP addresses which may call the API (on top of the firewall).</summary>
    public const string AllowedIpsVariableName = "OPENMU_WEBSITE_API_ALLOWED_IPS";

    /// <summary>The variable with the connection string of a read-only PostgreSQL role, used for all reads (optional).</summary>
    public const string ReadConnectionVariableName = "OPENMU_WEBSITE_API_READ_CONNECTION";

    /// <summary>The minimum length of an API secret.</summary>
    public const int MinimumSecretLength = 32;

    /// <summary>
    /// Gets the endpoint to listen on.
    /// </summary>
    public IPEndPoint EndPoint { get; init; } = new(IPAddress.Loopback, 8443);

    /// <summary>
    /// Gets the API keys: key id to secret.
    /// </summary>
    public IReadOnlyDictionary<string, string> Keys { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the path of the TLS certificate, or <c>null</c> for plain HTTP.
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>
    /// Gets the password of the TLS certificate.
    /// </summary>
    public string? CertificatePassword { get; init; }

    /// <summary>
    /// Gets the allowed caller addresses; empty means "everyone who can reach the port".
    /// </summary>
    public IReadOnlySet<IPAddress> AllowedIps { get; init; } = new HashSet<IPAddress>();

    /// <summary>
    /// Gets the optional connection string for reads (a read-only role). When <c>null</c>, the connection of the server itself is used.
    /// </summary>
    public string? ReadConnectionString { get; init; }

    /// <summary>
    /// Gets the maximum number of requests per minute from one client address.
    /// </summary>
    public int RequestsPerMinutePerIp { get; init; } = 120;

    /// <summary>
    /// Gets the maximum number of login-type attempts (verify, password change) per 10 minutes and account name.
    /// </summary>
    public int AuthAttemptsPer10MinutesPerAccount { get; init; } = 10;

    /// <summary>
    /// Gets the maximum number of registrations per hour from one client address.
    /// </summary>
    public int RegistrationsPerHourPerIp { get; init; } = 5;

    /// <summary>
    /// Parses the options from the environment.
    /// </summary>
    /// <param name="get">Reads an environment variable; <c>null</c> when it is not set.</param>
    /// <param name="error">The reason when the API is configured, but the configuration is unusable.</param>
    /// <returns>The options, or <c>null</c> when the API is off or the configuration is invalid (see <paramref name="error"/>).</returns>
    public static WebsiteApiOptions? FromEnvironment(Func<string, string?> get, out string? error)
    {
        error = null;
        var port = get(PortVariableName);
        if (string.IsNullOrWhiteSpace(port))
        {
            return null;
        }

        if (!int.TryParse(port, out var portNumber) || portNumber is < 1 or > 65535)
        {
            error = $"{PortVariableName} is not a valid port number.";
            return null;
        }

        var address = IPAddress.Any;
        if (get(AddressVariableName) is { Length: > 0 } addressText && !IPAddress.TryParse(addressText, out address))
        {
            error = $"{AddressVariableName} is not a valid IP address.";
            return null;
        }

        if (!TryParseKeys(get(KeysVariableName), out var keys, out error))
        {
            return null;
        }

        var certificatePath = get(CertificatePathVariableName);
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            certificatePath = null;
            if (!string.Equals(get(AllowHttpVariableName), "true", StringComparison.OrdinalIgnoreCase))
            {
                error = $"{CertificatePathVariableName} is not set; the API refuses to run without TLS unless {AllowHttpVariableName}=true.";
                return null;
            }
        }

        var allowed = new HashSet<IPAddress>();
        foreach (var part in (get(AllowedIpsVariableName) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPAddress.TryParse(part, out var ip))
            {
                error = $"{AllowedIpsVariableName} contains an invalid address: {part}";
                return null;
            }

            allowed.Add(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
        }

        return new WebsiteApiOptions
        {
            EndPoint = new IPEndPoint(address!, portNumber),
            Keys = keys,
            CertificatePath = certificatePath,
            CertificatePassword = get(CertificatePasswordVariableName),
            AllowedIps = allowed,
            ReadConnectionString = string.IsNullOrWhiteSpace(get(ReadConnectionVariableName)) ? null : get(ReadConnectionVariableName),
        };
    }

    private static bool TryParseKeys(string? text, out Dictionary<string, string> keys, out string? error)
    {
        keys = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = $"{KeysVariableName} is not set.";
            return false;
        }

        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf(':');
            if (separator <= 0)
            {
                error = $"{KeysVariableName} must look like keyId:secret[,keyId:secret].";
                return false;
            }

            var id = entry[..separator];
            var secret = entry[(separator + 1)..];
            if (Encoding.UTF8.GetByteCount(secret) < MinimumSecretLength)
            {
                error = $"The secret of API key '{id}' is shorter than {MinimumSecretLength} characters.";
                return false;
            }

            keys[id] = secret;
        }

        return true;
    }
}

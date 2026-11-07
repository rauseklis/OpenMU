// <copyright file="AccountRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Accounts;

using System.Net.Mail;
using System.Text.RegularExpressions;

/// <summary>
/// The limits for login names, passwords and e-mail addresses of new accounts, taken from what the game really accepts:
/// <list type="bullet">
/// <item>The login packet carries the login name in 10 bytes and the password in 20 bytes (<c>LoginLongPassword</c>;
/// the client has <c>MAX_USERNAME_SIZE 10</c> and <c>MAX_PASSWORD_SIZE 20</c>), and the database column
/// <c>Account.LoginName</c> is <c>varchar(10)</c>. The game configuration has <c>MaximumPasswordLength = 20</c>.</item>
/// <item>OpenMU has no rule for the characters of a login name; the website allows letters and digits only, 3 to 10 of them
/// (like the character name rule of the game, <c>^[a-zA-Z0-9]{3,10}$</c>), and treats names which differ only in case as the same.</item>
/// <item>Passwords: 8 to 20 printable ASCII characters without spaces (the login packet is transferred as bytes with a simple
/// obfuscation, so other characters are not safe for every client).</item>
/// </list>
/// </summary>
public static partial class AccountRules
{
    /// <summary>The minimum length of a login name.</summary>
    public const int MinimumLoginNameLength = 3;

    /// <summary>The maximum length of a login name (the size of the field in the login packet and of the database column).</summary>
    public const int MaximumLoginNameLength = 10;

    /// <summary>The minimum length of a password.</summary>
    public const int MinimumPasswordLength = 8;

    /// <summary>The maximum length of a password (the size of the field in the login packet).</summary>
    public const int MaximumPasswordLength = 20;

    /// <summary>The maximum length of an e-mail address.</summary>
    public const int MaximumEmailLength = 100;

    /// <summary>
    /// Checks the login name.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns><c>true</c> if valid.</returns>
    public static bool IsValidLoginName(string? loginName) => loginName is not null && LoginNameRegex().IsMatch(loginName);

    /// <summary>
    /// Checks the password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns><c>true</c> if valid.</returns>
    public static bool IsValidPassword(string? password)
    {
        return password is { Length: >= MinimumPasswordLength and <= MaximumPasswordLength }
            && password.All(c => c is > ' ' and <= '~');
    }

    /// <summary>
    /// Checks the e-mail address.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <returns><c>true</c> if valid.</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaximumEmailLength || email != email.Trim())
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var address) && address.Address == email && address.Host.Contains('.', StringComparison.Ordinal);
    }

    // \z, not $: "$" would also accept a trailing line feed.
    [GeneratedRegex(@"^[a-zA-Z0-9]{3,10}\z")]
    private static partial Regex LoginNameRegex();
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Identifies a key that a key management service holds: the name of the provider that reaches the service and the
/// service's own identifier for the key. The text form is <c>{Provider}:{KeyId}</c>.
/// </summary>
// See ADR-1471.
[PublicAPI]
public readonly partial record struct RemoteKeyReference
{
    // '\z' rather than '$', which also matches before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex ProviderNamePattern();

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteKeyReference"/> struct.
    /// </summary>
    /// <param name="provider">The name of the provider that reaches the key management service.</param>
    /// <param name="keyId">The key management service's identifier for the key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="provider"/> is not a valid provider name, or <paramref name="keyId"/> is empty.
    /// </exception>
    public RemoteKeyReference(string provider, string keyId)
    {
        if (!IsValidProviderName(provider))
        {
            throw new ArgumentException("The provider name is not valid.", nameof(provider));
        }

        if (string.IsNullOrEmpty(keyId))
        {
            throw new ArgumentException("The key identifier must not be empty.", nameof(keyId));
        }

        Provider = provider;
        KeyId = keyId;
    }

    /// <summary>
    /// Gets the name of the provider that reaches the key management service.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the key management service's identifier for the key.
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    /// Determines whether the specified value is a valid provider name: lowercase letters and digits in groups
    /// separated by single hyphens.
    /// </summary>
    /// <param name="name">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a valid provider name; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidProviderName([NotNullWhen(true)] string? name) =>
        name is not null && ProviderNamePattern().IsMatch(name);

    /// <summary>
    /// Parses the text form of a reference. The provider ends at the first <c>':'</c>, so a key identifier may itself
    /// hold <c>':'</c>, as an AWS ARN does.
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="reference">When this method returns <see langword="true"/>, the parsed reference.</param>
    /// <returns><see langword="true"/> if the text is a valid reference; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, out RemoteKeyReference reference)
    {
        reference = default;

        if (value is null)
        {
            return false;
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        var provider = value[..separator];
        var keyId = value[(separator + 1)..];
        if (!IsValidProviderName(provider) || keyId.Length == 0)
        {
            return false;
        }

        reference = new RemoteKeyReference(provider, keyId);
        return true;
    }

    /// <summary>
    /// Returns the text form of the reference, <c>{Provider}:{KeyId}</c>.
    /// </summary>
    /// <returns>The text form of the reference.</returns>
    public override string ToString() => $"{Provider}:{KeyId}";
}

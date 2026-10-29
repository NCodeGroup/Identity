#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Runtime.ExceptionServices;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides the signature policy and the signing-key resolution shared by the validation of the JWTs the provider issues
/// to the client (ADR-0273, ADR-0274, ADR-0301).
/// </summary>
internal static class ProviderSignaturePolicy
{
    /// <summary>
    /// Gets the registered asymmetric signature algorithms that the client permits and the provider advertises,
    /// excluding <c>none</c>. An empty or <see langword="null"/> list adds no restriction from its side.
    /// </summary>
    /// <param name="algorithms">The registered algorithms.</param>
    /// <param name="configured">The algorithms the client registered for the token.</param>
    /// <param name="advertised">The algorithms the provider advertises for the token.</param>
    public static List<string> GetPermittedAlgorithms(
        IAlgorithmCollection algorithms,
        IReadOnlyList<string>? configured,
        IReadOnlyList<string>? advertised
    )
    {
        var result = new List<string>();
        foreach (var algorithm in algorithms)
        {
            if (
                algorithm is not SignatureAlgorithm signatureAlgorithm
                || !typeof(AsymmetricSecretKey).IsAssignableFrom(signatureAlgorithm.KeyType)
                || string.Equals(
                    signatureAlgorithm.Code,
                    AlgorithmCodes.DigitalSignature.None,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;

            var code = signatureAlgorithm.Code;
            if (configured is { Count: > 0 } && !configured.Contains(code, StringComparer.Ordinal))
                continue;

            if (advertised is { Count: > 0 } && !advertised.Contains(code, StringComparer.Ordinal))
                continue;

            result.Add(code);
        }

        return result;
    }

    /// <summary>
    /// Resolves the provider's signing keys for a token, selecting by <c>kid</c> and refreshing the key set once when
    /// the <c>kid</c> is unknown.
    /// </summary>
    public static async ValueTask<IEnumerable<SecretKey>> ResolveKeysAsync(
        IProviderKeySetResolver keySetResolver,
        string authority,
        JoseToken token,
        CancellationToken cancellationToken
    )
    {
        var keys = await keySetResolver.GetSigningKeysAsync(authority, cancellationToken);

        if (
            !token.Header.TryGetProperty(JoseClaimNames.Header.Kid, out var kidElement)
            || kidElement.ValueKind != JsonValueKind.String
            || kidElement.GetString() is not { Length: > 0 } kid
        )
            return keys;

        if (keys.TryGetByKeyId(kid, out var key))
            return [key];

        keys = await keySetResolver.RefreshSigningKeysAsync(authority, cancellationToken);
        return keys.TryGetByKeyId(kid, out key) ? [key] : [];
    }

    /// <summary>
    /// Rethrows a validation failure that is a cancellation or a provider failure, preserving its stack trace, because
    /// the validators wrap every failure, including those raised while resolving keys.
    /// </summary>
    public static void RethrowProviderFailure(Exception exception)
    {
        if (exception is OperationCanceledException or OpenIdProviderException)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}

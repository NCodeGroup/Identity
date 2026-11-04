// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Resolves the recipients of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c> message and runs their key distribution
/// algorithms: direct use of the recipient key, AES key wrap and RSAES-OAEP key transport. None of the registered
/// algorithms reads a JOSE header or a recipient context, so an empty header is passed.
/// </summary>
// See ADR-1409.
internal static class CoseContentKeys
{
    /// <summary>
    /// Why a recipient is not eligible for the caller.
    /// </summary>
    public enum RecipientSkip
    {
        None,
        NotPermitted,
        Unsupported,
    }

    /// <summary>
    /// Gets the empty JOSE header handed to the key distribution algorithms.
    /// </summary>
    public static JsonElement EmptyJoseHeader { get; } = JsonElement.Parse("{}");

    /// <summary>
    /// Resolves the key distribution algorithm of a recipient, which is in its unprotected bucket.
    /// </summary>
    /// <returns>The binding, or <c>null</c> with the reason the recipient is skipped.</returns>
    /// <exception cref="CoseException">The recipient names no algorithm.</exception>
    public static CoseAlgorithmBinding? ResolveRecipient(
        ParsedCoseLayer recipient,
        IReadOnlyCollection<int> permitted,
        ICoseAlgorithmCatalog catalog,
        out RecipientSkip skip
    )
    {
        skip = RecipientSkip.None;
        if (!recipient.Unprotected.TryGetValue(CoseHeaderLabels.Algorithm, out var value))
            throw new CoseException(
                CoseFailureCodes.InvalidHeader,
                "A recipient names no algorithm."
            );

        if (value.TryGetTextString(out _))
        {
            skip = RecipientSkip.Unsupported;
            return null;
        }

        if (
            !value.TryGetInt64(out var identifier)
            || identifier is < int.MinValue or > int.MaxValue
            || !permitted.Contains((int)identifier)
        )
        {
            skip = RecipientSkip.NotPermitted;
            return null;
        }

        if (
            !catalog.TryGet((int)identifier, out var binding)
            || binding.Algorithm.Kind != CoseAlgorithmKind.KeyDistribution
        )
        {
            skip = RecipientSkip.Unsupported;
            return null;
        }

        return binding;
    }

    /// <summary>
    /// Unwraps a content key with a candidate recipient key. Any failure of the key distribution algorithm means that
    /// the key does not yield the content key.
    /// </summary>
    public static bool TryUnwrap(
        KeyManagementAlgorithm algorithm,
        SecretKey key,
        ReadOnlySpan<byte> ciphertext,
        Span<byte> destination,
        out int written
    )
    {
        try
        {
            return algorithm.TryUnwrapKey(
                key,
                EmptyJoseHeader,
                ciphertext,
                destination,
                out written
            );
        }
        catch (Exception exception)
            when (exception
                    is JoseException
                        or CryptographicException
                        or ArgumentException
                        or InvalidOperationException
            )
        {
            written = 0;
            return false;
        }
    }

    /// <summary>
    /// Finds the key distribution algorithm of a recipient, ensuring that it is a registered key distribution
    /// algorithm that suits the key.
    /// </summary>
    public static bool TryGetKeyDistribution(
        CoseCredentials credentials,
        ICoseAlgorithmCatalog catalog,
        [NotNullWhen(true)] out KeyManagementAlgorithm? algorithm
    )
    {
        algorithm = null;
        if (
            !catalog.TryGet(credentials.Algorithm.Identifier, out var binding)
            || binding.Algorithm.Kind != CoseAlgorithmKind.KeyDistribution
            || binding.JoseAlgorithm is not KeyManagementAlgorithm candidate
            || !candidate.IsKeyCompatible(credentials.SecretKey)
        )
            return false;

        algorithm = candidate;
        return true;
    }
}

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

using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretValidator"/> that rejects incoherent
/// <c>secretType</c> / <c>keySizeBits</c> / <c>use</c> / <c>algorithm</c> combinations with a
/// deterministic <c>400</c> before any key material is generated.
/// </summary>
/// <remarks>
/// Algorithm compatibility is resolved against the live <see cref="IAlgorithmCollectionProvider"/> rather
/// than a hardcoded table, so the validator cannot drift from the set of algorithms the server actually
/// supports (including any custom algorithms an integrator registers): the same <see cref="KeyedAlgorithm"/>
/// metadata the crypto runtime uses to validate a key is what gates its creation here.
/// </remarks>
internal sealed class DefaultSecretValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider
) : ISecretValidator
{
    // The generatable secret families and the CLR key type each maps to. This is the intrinsic secret
    // taxonomy (three families), not the algorithm set, so it does not drift as algorithms are added.
    private static readonly FrozenDictionary<string, Type> SecretKeyClrTypes = new Dictionary<
        string,
        Type
    >(StringComparer.Ordinal)
    {
        [SecretTypes.Symmetric] = typeof(SymmetricSecretKey),
        [SecretTypes.Rsa] = typeof(RsaSecretKey),
        [SecretTypes.Ecc] = typeof(EccSecretKey),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    /// <inheritdoc />
    public ManagementError? ValidateCreate(CreateSecretRequest request)
    {
        var secretType = request.SecretType;
        if (!SecretKeyClrTypes.TryGetValue(secretType, out var secretKeyClrType))
        {
            return Error(
                $"The secret type '{secretType}' is not supported; expected "
                    + $"'{SecretTypes.Symmetric}', '{SecretTypes.Rsa}', or '{SecretTypes.Ecc}'."
            );
        }

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Error(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        // When an algorithm is specified, its registered metadata is authoritative for the use, the
        // required key type, and the legal key sizes.
        if (request.Algorithm is { } algorithm)
        {
            return ValidateAgainstAlgorithm(request, secretType, secretKeyClrType, use, algorithm);
        }

        // Without an algorithm, fall back to the generator's intrinsic per-family key-size capabilities.
        return ValidateKeySize(secretType, request.KeySizeBits);
    }

    private ManagementError? ValidateAgainstAlgorithm(
        CreateSecretRequest request,
        string secretType,
        Type secretKeyClrType,
        string? use,
        string algorithm
    )
    {
        var collection = AlgorithmCollectionProvider.Collection;

        KeyedAlgorithm keyedAlgorithm;
        string impliedUse;
        if (collection.TryGetSignatureAlgorithm(algorithm, out var signatureAlgorithm))
        {
            keyedAlgorithm = signatureAlgorithm;
            impliedUse = SecretKeyUses.Signature;
        }
        else if (collection.TryGetKeyManagementAlgorithm(algorithm, out var keyManagementAlgorithm))
        {
            keyedAlgorithm = keyManagementAlgorithm;
            impliedUse = SecretKeyUses.Encryption;
        }
        else
        {
            return Error(
                $"The algorithm '{algorithm}' is not a supported signature or key-management algorithm."
            );
        }

        if (use is not null && !string.Equals(use, impliedUse, StringComparison.Ordinal))
        {
            return Error(
                $"The algorithm '{algorithm}' is intended for '{impliedUse}' use and is "
                    + $"incompatible with the specified use '{use}'."
            );
        }

        if (!keyedAlgorithm.KeyType.IsAssignableFrom(secretKeyClrType))
        {
            return Error(
                $"The algorithm '{algorithm}' is not compatible with a '{secretType}' secret."
            );
        }

        if (!KeySizesUtility.IsLegalSize(keyedAlgorithm.KeyBitSizes, request.KeySizeBits))
        {
            return Error(
                $"The key size '{request.KeySizeBits}' bits is not valid for algorithm '{algorithm}'."
            );
        }

        return null;
    }

    // Mirrors the hard limits of DefaultSecretGenerator for the case where no algorithm pins the size.
    private static ManagementError? ValidateKeySize(string secretType, int keySizeBits) =>
        secretType switch
        {
            SecretTypes.Symmetric when keySizeBits < 128 || keySizeBits % 8 != 0 => Error(
                $"The symmetric key size '{keySizeBits}' bits is invalid; expected a multiple of 8 "
                    + "that is at least 128 bits."
            ),
            SecretTypes.Rsa
                when keySizeBits < 2048 || keySizeBits > 16384 || keySizeBits % 8 != 0 => Error(
                $"The RSA key size '{keySizeBits}' bits is invalid; expected a multiple of 8 "
                    + "between 2048 and 16384 bits."
            ),
            SecretTypes.Ecc when keySizeBits is not (256 or 384 or 521) => Error(
                $"The ECC key size '{keySizeBits}' bits is invalid; expected 256, 384, or 521."
            ),
            _ => null,
        };

    private static ManagementError Error(string detail) =>
        new() { StatusCode = StatusCodes.Status400BadRequest, Detail = detail };
}

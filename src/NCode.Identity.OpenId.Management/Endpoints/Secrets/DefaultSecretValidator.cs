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

using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretValidator"/> that rejects incoherent
/// <c>secretType</c> / <c>keySizeBits</c> / <c>use</c> / <c>algorithm</c> combinations with a
/// deterministic <c>400</c> before any key material is generated.
/// </summary>
/// <remarks>
/// The key-type taxonomy and intrinsic per-type key-size rules come from the <see cref="ISecretKeyTypeCatalog"/>
/// (the single source of truth), so the validator cannot drift from the types the generator can actually
/// produce. Algorithm compatibility is resolved against the live <see cref="IAlgorithmCollectionProvider"/> rather
/// than a hardcoded table, so the validator also cannot drift from the set of algorithms the server actually
/// supports (including any custom algorithms an integrator registers): the same <see cref="KeyedAlgorithm"/>
/// metadata the crypto runtime uses to validate a key is what gates its creation here.
/// </remarks>
// See ADR-0067.
internal sealed class DefaultSecretValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ISecretValidator
{
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public ManagementError? ValidateCreate(CreateSecretRequest request)
    {
        var secretType = request.SecretType;
        if (!SecretKeyTypeCatalog.TryGet(secretType, out var descriptor))
        {
            return Error(
                $"The secret type '{secretType}' is not supported or cannot be created through the "
                    + "management API."
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

        // The descriptor validates the intrinsic generation parameters (key size and, for ECC, the curve).
        var generationError = descriptor.ValidateGeneration(
            new SecretKeyGenerationParameters
            {
                KeySizeBits = request.KeySizeBits,
                Curve = request.Curve,
            }
        );
        if (generationError is not null)
        {
            return Error(generationError);
        }

        // When an algorithm is specified, its registered metadata is authoritative for the use, the
        // required key type, and the legal key sizes.
        if (request.Algorithm is { } algorithm)
        {
            var algorithmError = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
                new KeyAlgorithmDeclaration
                {
                    KeyType = descriptor.KeyClrType,
                    KeySizeBits = request.KeySizeBits,
                    Use = use,
                    Algorithm = algorithm,
                }
            );
            return algorithmError is null ? null : Error(algorithmError);
        }

        return null;
    }

    /// <inheritdoc />
    public ManagementError? ValidateImport(ImportSecretRequest request)
    {
        // Each key type declares the formats it parses, so a key type another package registers brings its own (ADR-0150).
        var secretType = request.SecretType;
        if (
            !SecretKeyTypeCatalog.TryGet(secretType, out var descriptor)
            || descriptor.PublicKeyImportFormats.Count == 0
        )
        {
            return Error(
                $"The secret type '{secretType}' is not supported or cannot be imported as public key material."
            );
        }

        if (!descriptor.PublicKeyImportFormats.Contains(request.Format))
        {
            return Error(
                $"The import format '{request.Format}' is not supported for the secret type '{secretType}'; expected "
                    + string.Join(
                        ", ",
                        descriptor.PublicKeyImportFormats.Select(format => $"'{format}'")
                    )
                    + "."
            );
        }

        if (string.IsNullOrWhiteSpace(request.KeyMaterial))
        {
            return Error("The key material must not be empty.");
        }

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Error(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        return null;
    }

    /// <inheritdoc />
    // See ADR-0161 and ADR-0186.
    public ManagementError? ValidateUpdate(PersistedSecret secret, UpdateSecretRequest request)
    {
        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            return Error(
                $"The use '{use}' is not supported; expected "
                    + $"'{SecretKeyUses.Signature}' or '{SecretKeyUses.Encryption}'."
            );
        }

        if (request.Algorithm is not { } algorithm)
            return null;

        if (!SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor))
            return Error($"The secret type '{secret.SecretType}' is not supported.");

        var algorithmError = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = descriptor.KeyClrType,
                KeySizeBits = secret.KeySizeBits,
                Use = use,
                Algorithm = algorithm,
            }
        );
        return algorithmError is null ? null : Error(algorithmError);
    }

    private static ManagementError Error(string detail) =>
        new() { StatusCode = StatusCodes.Status400BadRequest, Detail = detail };
}

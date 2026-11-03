// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides extension members for <see cref="IAlgorithmCollection"/>.
/// </summary>
[PublicAPI]
public static class AlgorithmCollectionExtensions
{
    private static string FormatCurveError(
        string algorithm,
        string? curve,
        int? keySizeBits,
        AlgorithmKeyType algorithmKeyType
    )
    {
        var subject = curve is null ? "A key without a named curve" : $"The curve '{curve}'";
        var size = keySizeBits is { } bits ? $" with a key size of '{bits}' bits" : string.Empty;
        return $"{subject}{size} is not valid for algorithm '{algorithm}'; expected "
            + FormatCurves(algorithmKeyType)
            + ".";
    }

    private static string FormatCurves(AlgorithmKeyType algorithmKeyType) =>
        string.Join(
            ", ",
            algorithmKeyType.Curves.Select(legal => $"'{legal.Name}' ({legal.KeySizeBits} bits)")
        );

    /// <param name="collection">The <see cref="IAlgorithmCollection"/> of registered algorithms.</param>
    extension(IAlgorithmCollection collection)
    {
        /// <summary>
        /// Gets the registered signature or key-management algorithm with the specified code, the algorithms a key is
        /// declared for, together with the use it implies: <c>sig</c> for a signature algorithm and <c>enc</c> for a
        /// key-management algorithm.
        /// </summary>
        /// <param name="code">The algorithm's code.</param>
        /// <param name="algorithm">When this method returns <c>true</c>, the algorithm.</param>
        /// <param name="impliedUse">When this method returns <c>true</c>, the use the algorithm implies.</param>
        /// <returns><c>true</c> when a signature or key-management algorithm is registered with the code; otherwise,
        /// <c>false</c>.</returns>
        public bool TryGetKeyedAlgorithm(
            string code,
            [NotNullWhen(true)] out KeyedAlgorithm? algorithm,
            [NotNullWhen(true)] out string? impliedUse
        )
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (collection.TryGetSignatureAlgorithm(code, out var signatureAlgorithm))
            {
                algorithm = signatureAlgorithm;
                impliedUse = SecretKeyUses.Signature;
                return true;
            }

            if (collection.TryGetKeyManagementAlgorithm(code, out var keyManagementAlgorithm))
            {
                algorithm = keyManagementAlgorithm;
                impliedUse = SecretKeyUses.Encryption;
                return true;
            }

            algorithm = null;
            impliedUse = null;
            return false;
        }

        /// <summary>
        /// Checks that a key may be declared for an algorithm: the algorithm is a registered signature or
        /// key-management algorithm, the use it implies (<c>sig</c> for a signature algorithm, <c>enc</c> for a
        /// key-management algorithm) agrees with the key's declared use, and the algorithm accepts the key, checked by
        /// <see cref="KeyedAlgorithm.IsKeyCompatible"/>, the check the cryptographic runtime makes before it uses the
        /// key. The key's type, size and curve are read from the key, so two curves that share a size, as P-256 and
        /// secp256k1 or Ed25519 and X25519 do, are told apart.
        /// </summary>
        /// <param name="declaration">The key and the use and algorithm declared for it.</param>
        /// <returns><c>null</c> when the declaration is coherent; otherwise a human-readable reason.</returns>
        public string? ValidateKeyAlgorithm(KeyAlgorithmDeclaration declaration)
        {
            ArgumentNullException.ThrowIfNull(collection);

            var algorithm = declaration.Algorithm;
            var key = declaration.Key;
            ArgumentNullException.ThrowIfNull(key, nameof(declaration));

            if (
                !collection.TryGetDeclaredAlgorithm(
                    algorithm,
                    declaration.Use,
                    out var keyedAlgorithm,
                    out _,
                    out var error
                )
            )
            {
                return error;
            }

            if (!keyedAlgorithm.TryGetKeyType(key.GetType(), out var algorithmKeyType))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key's type.";
            }

            if (!algorithmKeyType.IsLegalSize(key.KeySizeBits))
            {
                return $"The key size '{key.KeySizeBits}' bits is not valid for algorithm '{algorithm}'.";
            }

            // See ADR-1389: the curve separates keys that share a type and a size, such as a signing and an agreement
            // OKP key, which RFC 8037 §3.1 and §3.2 keep apart.
            var curve = (key as AsymmetricSecretKey)?.CurveName;
            if (!algorithmKeyType.IsLegalCurve(curve, key.KeySizeBits))
            {
                return FormatCurveError(algorithm, curve, key.KeySizeBits, algorithmKeyType);
            }

            if (!keyedAlgorithm.IsKeyCompatible(key))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key, whose parameters it does not "
                    + "accept.";
            }

            return null;
        }

        /// <summary>
        /// Resolves a key to generate for an algorithm: the algorithm is a registered signature or key-management
        /// algorithm, the use it implies agrees with the declared use, and the algorithm accepts keys of the declared
        /// runtime type. For a key type on curves, the key is generated on the declared curve, which must be one the
        /// algorithm accepts, or, when none is declared, on the first curve the algorithm accepts that has the declared
        /// size, or on its first curve when no size is declared either; the key's size is its curve's. For a key type
        /// without curves, the declared size must be one the algorithm accepts, and the smallest it accepts is taken
        /// when none is declared. Two curves can share a key size, so a key is never generated on whichever curve the
        /// key type picks for the size.
        /// </summary>
        /// <param name="declaration">The key to generate and the use and algorithm declared for it.</param>
        /// <param name="resolved">When this method returns <c>true</c>, the resolved key.</param>
        /// <param name="error">When this method returns <c>false</c>, a human-readable reason.</param>
        /// <returns><c>true</c> when the declaration resolves; otherwise, <c>false</c>.</returns>
        public bool TryResolveKeyGeneration(
            KeyGenerationDeclaration declaration,
            [NotNullWhen(true)] out ResolvedKeyGeneration? resolved,
            [NotNullWhen(false)] out string? error
        )
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(declaration.KeyClrType, nameof(declaration));

            resolved = null;
            var algorithm = declaration.Algorithm;

            if (
                !collection.TryGetDeclaredAlgorithm(
                    algorithm,
                    declaration.Use,
                    out var keyedAlgorithm,
                    out var impliedUse,
                    out error
                )
            )
            {
                return false;
            }

            if (!keyedAlgorithm.TryGetKeyType(declaration.KeyClrType, out var algorithmKeyType))
            {
                error = $"The algorithm '{algorithm}' is not compatible with the key's type.";
                return false;
            }

            int keySizeBits;
            var curve = declaration.Curve;
            if (algorithmKeyType.Curves.Count > 0)
            {
                // See ADR-1389 and ADR-1392: the curve, not the size, identifies the key an algorithm bound to curves
                // accepts.
                AlgorithmCurve? algorithmCurve;
                if (curve is not null)
                {
                    if (
                        !algorithmKeyType.TryGetCurve(curve, out algorithmCurve)
                        || (
                            declaration.KeySizeBits is { } size
                            && size != algorithmCurve.KeySizeBits
                        )
                    )
                    {
                        error = FormatCurveError(
                            algorithm,
                            curve,
                            declaration.KeySizeBits ?? algorithmCurve?.KeySizeBits,
                            algorithmKeyType
                        );
                        return false;
                    }
                }
                else if (declaration.KeySizeBits is { } size)
                {
                    if (!algorithmKeyType.TryGetCurve(size, out algorithmCurve))
                    {
                        error =
                            $"The key size '{size}' bits is not valid for algorithm '{algorithm}'; expected "
                            + FormatCurves(algorithmKeyType)
                            + ".";
                        return false;
                    }
                }
                else
                {
                    algorithmCurve = algorithmKeyType.Curves[0];
                }

                keySizeBits = algorithmCurve.KeySizeBits;
                curve = algorithmCurve.Name;
            }
            else if (declaration.KeySizeBits is { } size)
            {
                if (!algorithmKeyType.IsLegalSize(size))
                {
                    error = $"The key size '{size}' bits is not valid for algorithm '{algorithm}'.";
                    return false;
                }

                keySizeBits = size;
            }
            else if (algorithmKeyType.KeyBitSizes.Count > 0)
            {
                keySizeBits = algorithmKeyType.KeyBitSizes.Min(legalSize => legalSize.MinSize);
            }
            else
            {
                error = $"The algorithm '{algorithm}' accepts no key size.";
                return false;
            }

            resolved = new ResolvedKeyGeneration
            {
                Algorithm = keyedAlgorithm,
                Use = impliedUse,
                AlgorithmKeyType = algorithmKeyType,
                KeySizeBits = keySizeBits,
                Curve = curve,
            };
            error = null;
            return true;
        }

        private bool TryGetDeclaredAlgorithm(
            string algorithm,
            string? use,
            [NotNullWhen(true)] out KeyedAlgorithm? keyedAlgorithm,
            [NotNullWhen(true)] out string? impliedUse,
            [NotNullWhen(false)] out string? error
        )
        {
            if (!collection.TryGetKeyedAlgorithm(algorithm, out keyedAlgorithm, out impliedUse))
            {
                error =
                    $"The algorithm '{algorithm}' is not a supported signature or key-management algorithm.";
                return false;
            }

            if (use is not null && !string.Equals(use, impliedUse, StringComparison.Ordinal))
            {
                error =
                    $"The algorithm '{algorithm}' is intended for '{impliedUse}' use and is incompatible with the "
                    + $"specified use '{use}'.";
                keyedAlgorithm = null;
                impliedUse = null;
                return false;
            }

            error = null;
            return true;
        }
    }
}

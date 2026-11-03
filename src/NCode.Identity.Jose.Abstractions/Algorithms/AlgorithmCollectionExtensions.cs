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
        /// Checks that a held symmetric key, known by its type and size because its material is not at hand, as for a
        /// stored key, may be declared for an algorithm: the algorithm is a registered signature or
        /// key-management algorithm, the use it implies agrees with the key's declared use, and the algorithm accepts
        /// keys of the type and size. A symmetric key is a byte string, so its type and size are all there is to check.
        /// An asymmetric key has parameters its type and size do not show, such as its curve or its parameter set, and
        /// its public half is always at hand, so an asymmetric key type, and a key type the algorithm binds to curves,
        /// is refused; such a key is checked by the key itself, with <c>ValidateKeyAlgorithm</c>.
        /// </summary>
        /// <param name="declaration">The key's type and size, and the use and algorithm declared for it.</param>
        /// <returns><c>null</c> when the declaration is coherent; otherwise a human-readable reason.</returns>
        public string? ValidateKeySize(KeySizeDeclaration declaration)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(declaration.KeyClrType, nameof(declaration));

            var algorithm = declaration.Algorithm;
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

            if (!keyedAlgorithm.TryGetKeyType(declaration.KeyClrType, out var algorithmKeyType))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key's type.";
            }

            // See ADR-1398: only a symmetric key's type and size describe it whole.
            if (algorithmKeyType.Curves.Count > 0)
            {
                return $"The algorithm '{algorithm}' accepts keys of this type only on its curves, which the key's "
                    + "type and size do not show.";
            }

            // A key type states that its keys are asymmetric by deriving from AsymmetricSecretKey, the base that holds
            // a key's curve name and public half and the fact ISecretKeyTypeDescriptor.IsAsymmetric reports, so the
            // key's runtime type is asked, as ValidateKeyAlgorithm asks it for the key's curve.
            if (typeof(AsymmetricSecretKey).IsAssignableFrom(declaration.KeyClrType))
            {
                return $"The algorithm '{algorithm}' is checked against an asymmetric key by the key itself, whose "
                    + "parameters its type and size do not show.";
            }

            return algorithmKeyType.IsLegalSize(declaration.KeySizeBits)
                ? null
                : $"The key size '{declaration.KeySizeBits}' bits is not valid for algorithm '{algorithm}'.";
        }

        /// <summary>
        /// Resolves a key to generate for an algorithm: the algorithm is a registered signature or key-management
        /// algorithm, the use it implies agrees with the declared use, and the algorithm accepts keys of the declared
        /// runtime type. For a key type on curves, the key is generated on the declared curve, which must be one the
        /// algorithm accepts, or, when none is declared, on the one curve the algorithm accepts that has the declared
        /// size, or on its first curve when no size is declared either. Two curves can share a key size, so a declared
        /// size that several of the algorithm's curves have is refused, and the curve must then be declared. The
        /// resolved size is the declared size, or the curve's when none is declared; the key type's
        /// <c>ISecretKeyTypeDescriptor.ValidateGeneration</c> refuses a size that is not its curve's, as for any
        /// generation parameters. For a key type without curves, no curve may be declared, and the declared size must
        /// be one the algorithm accepts, the smallest it accepts being taken when none is declared.
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
                // See ADR-1389, ADR-1392 and ADR-1398: the curve, not the size, identifies the key an algorithm bound
                // to curves accepts, so a size selects a curve only when exactly one of the algorithm's curves has it.
                AlgorithmCurve? algorithmCurve;
                if (curve is not null)
                {
                    if (!algorithmKeyType.TryGetCurve(curve, out algorithmCurve))
                    {
                        error = FormatCurveError(
                            algorithm,
                            curve,
                            keySizeBits: null,
                            algorithmKeyType
                        );
                        return false;
                    }
                }
                else if (declaration.KeySizeBits is { } size)
                {
                    var curves = algorithmKeyType.GetCurves(size);
                    if (curves.Count == 0)
                    {
                        error =
                            $"The key size '{size}' bits is not valid for algorithm '{algorithm}'; expected "
                            + FormatCurves(algorithmKeyType)
                            + ".";
                        return false;
                    }

                    if (curves.Count > 1)
                    {
                        error =
                            $"The key size '{size}' bits does not select a curve for algorithm '{algorithm}', which "
                            + "accepts "
                            + string.Join(", ", curves.Select(legal => $"'{legal.Name}'"))
                            + " at that size; name the curve.";
                        return false;
                    }

                    algorithmCurve = curves[0];
                }
                else
                {
                    algorithmCurve = algorithmKeyType.Curves[0];
                }

                // See ADR-1398: a curve fixes the size of its keys, which the key type's descriptor checks when it
                // validates the generation parameters, so a declared size is passed on to it rather than replaced.
                keySizeBits = declaration.KeySizeBits ?? algorithmCurve.KeySizeBits;
                curve = algorithmCurve.Name;
            }
            else if (curve is not null)
            {
                error =
                    $"The algorithm '{algorithm}' names no curve for keys of this type, so the curve '{curve}' cannot "
                    + "be declared.";
                return false;
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

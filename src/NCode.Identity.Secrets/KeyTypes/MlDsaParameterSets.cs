// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// The closed catalog of ML-DSA (FIPS 204) parameter sets supported by the <c>AKP</c> secret-key type:
/// <c>ML-DSA-44</c>, <c>ML-DSA-65</c>, and <c>ML-DSA-87</c>. The JOSE / JWK <c>alg</c> identifier is the BCL
/// <see cref="MLDsaAlgorithm.Name"/>.
/// </summary>
internal static class MlDsaParameterSets
{
    /// <summary>Gets all supported ML-DSA parameter sets.</summary>
    public static IReadOnlyList<MLDsaAlgorithm> All { get; } =
    [MLDsaAlgorithm.MLDsa44, MLDsaAlgorithm.MLDsa65, MLDsaAlgorithm.MLDsa87];

    /// <summary>Resolves a parameter set by its <c>alg</c> name (for example <c>ML-DSA-65</c>).</summary>
    public static bool TryGetByName(string? name, [NotNullWhen(true)] out MLDsaAlgorithm? algorithm)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                algorithm = candidate;
                return true;
            }
        }

        algorithm = null;
        return false;
    }

    /// <summary>Resolves a parameter set by its public-key size in bits.</summary>
    public static bool TryGetByKeySizeBits(
        int keySizeBits,
        [NotNullWhen(true)] out MLDsaAlgorithm? algorithm
    )
    {
        foreach (var candidate in All)
        {
            if (candidate.PublicKeySizeInBytes * 8 == keySizeBits)
            {
                algorithm = candidate;
                return true;
            }
        }

        algorithm = null;
        return false;
    }
}

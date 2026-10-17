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

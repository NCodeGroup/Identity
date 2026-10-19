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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Classifies the registered algorithms that the algorithm settings (<c>*_alg_values_supported</c>,
/// <c>*_enc_values_supported</c>, <c>*_zip_values_supported</c>) may name. The server's advertised defaults and the
/// values those settings accept are both derived from this one classification, so they cannot drift apart.
/// </summary>
internal static class SettingAlgorithms
{
    /// <summary>
    /// Determines whether an algorithm can be offered through an algorithm setting. <c>none</c> (an unsigned JWS) and
    /// <c>dir</c> (a shared key used directly as the content-encryption key) never are.
    /// </summary>
    /// <param name="algorithm">The registered algorithm.</param>
    /// <returns><see langword="true"/> when the algorithm can be offered; otherwise, <see langword="false"/>.</returns>
    public static bool IsSelectable(Algorithm algorithm) =>
        !string.IsNullOrEmpty(algorithm.Code)
        && !string.Equals(
            algorithm.Code,
            AlgorithmCodes.DigitalSignature.None,
            StringComparison.Ordinal
        )
        && !string.Equals(
            algorithm.Code,
            AlgorithmCodes.KeyManagement.Direct,
            StringComparison.Ordinal
        );

    /// <summary>
    /// Determines whether an algorithm is a digital-signature algorithm keyed by an asymmetric key — the only kind a
    /// token that crosses a trust boundary (an ID token, a UserInfo response, an access token, a DPoP proof) is signed
    /// with.
    /// </summary>
    /// <param name="algorithm">The registered algorithm.</param>
    /// <returns><see langword="true"/> when the algorithm is an asymmetric signature algorithm; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool IsAsymmetricSignature(Algorithm algorithm) =>
        algorithm.Type == AlgorithmType.DigitalSignature
        && algorithm is KeyedAlgorithm keyedAlgorithm
        && typeof(AsymmetricSecretKey).IsAssignableFrom(keyedAlgorithm.KeyType);
}

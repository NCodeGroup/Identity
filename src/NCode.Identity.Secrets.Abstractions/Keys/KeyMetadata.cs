#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Contains common metadata for a secret key such as <c>KeyId</c>, <c>Use</c>, <c>Algorithm</c>, and <c>ExpiresWhen</c>.
/// </summary>
/// <remarks>
/// <para>
/// This structure holds the standard metadata properties defined in the JSON Web Key (JWK) specification
/// (RFC 7517) that describe how a cryptographic key should be identified and used.
/// </para>
/// <para>
/// All properties are optional and nullable, allowing for flexible key configurations where
/// metadata may be partially specified or omitted entirely.
/// </para>
/// </remarks>
/// <seealso href="https://tools.ietf.org/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
[PublicAPI]
public readonly record struct KeyMetadata
{
    /// <summary>
    /// Gets or sets the <c>Key ID (kid)</c> for the secret key.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies the key, or <c>null</c> if no identifier is specified.
    /// </value>
    /// <remarks>
    /// The Key ID is used to match a specific key when multiple keys are available.
    /// This corresponds to the <c>kid</c> parameter defined in RFC 7517 Section 4.5.
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.5">RFC 7517 Section 4.5</seealso>
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets or sets the intended use for the secret key.
    /// </summary>
    /// <value>
    /// A string indicating the intended use of the key, or <c>null</c> to indicate the key
    /// may be used with any compatible operation.
    /// </value>
    /// <remarks>
    /// <para>
    /// This corresponds to the <c>use</c> parameter defined in RFC 7517 Section 4.2.
    /// Common values include:
    /// </para>
    /// <list type="bullet">
    ///     <item><description><c>sig</c> — The key is intended for digital signatures.</description></item>
    ///     <item><description><c>enc</c> — The key is intended for encryption.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.2">RFC 7517 Section 4.2</seealso>
    public string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for the secret key.
    /// </summary>
    /// <value>
    /// A string identifying the algorithm intended for use with the key, or <c>null</c> to indicate
    /// the key may be used with any compatible algorithm.
    /// </value>
    /// <remarks>
    /// <para>
    /// This corresponds to the <c>alg</c> parameter defined in RFC 7517 Section 4.4.
    /// When specified, this identifies the specific cryptographic algorithm the key is intended for
    /// (e.g., <c>RS256</c>, <c>ES384</c>, <c>A128GCM</c>).
    /// </para>
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7517#section-4.4">RFC 7517 Section 4.4</seealso>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets or sets the expiration time for the secret key.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> indicating when the key expires and is no longer valid,
    /// or <c>null</c> to indicate the key never expires.
    /// </value>
    /// <remarks>
    /// When set, the key should not be used for cryptographic operations after this time.
    /// Key rotation policies typically use this property to manage key lifecycles.
    /// </remarks>
    public DateTimeOffset? ExpiresWhen { get; init; }
}

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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose;

/// <summary>
/// Contains the options that are required when encoding a JWS token, for any serialization format.
/// The compact serialization is the degenerate case of exactly one signature with no unprotected header;
/// the flattened and general JSON serializations additionally support unprotected header members, and the
/// general JSON serialization supports multiple signatures.
/// </summary>
[PublicAPI]
public sealed class JoseSigningOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSigningOptions"/> class. The <see cref="Signatures"/>
    /// property must be set via an object initializer.
    /// </summary>
    public JoseSigningOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSigningOptions"/> class with a single signature.
    /// </summary>
    /// <param name="signature">The single signature to produce.</param>
    [SetsRequiredMembers]
    public JoseSigningOptions(JoseSignatureOptions signature)
    {
        Signatures = [signature];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSigningOptions"/> class with a single signature using
    /// the specified credentials and default per-signature options.
    /// </summary>
    /// <param name="signingCredentials">The credentials used to produce the single signature.</param>
    [SetsRequiredMembers]
    public JoseSigningOptions(JoseSigningCredentials signingCredentials)
        : this(new JoseSignatureOptions(signingCredentials)) { }

    /// <summary>
    /// Gets the serialization format to produce. The default value is <see cref="JoseSerializationFormat.Compact"/>.
    /// The <see cref="JoseSerializationFormat.Compact"/> and <see cref="JoseSerializationFormat.Flattened"/> formats
    /// require exactly one signature.
    /// </summary>
    public JoseSerializationFormat Format { get; init; } = JoseSerializationFormat.Compact;

    /// <summary>
    /// Gets a value indicating whether the payload should be base64url encoded (default).
    /// When <c>false</c>, the unencoded-payload option from <c>RFC 7797</c> is used and the <c>b64</c> header
    /// is emitted (and marked critical) in every signature's protected header. The payload is shared by all
    /// signatures, so this applies token-wide.
    /// </summary>
    public bool EncodePayload { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the payload should be omitted (detached) from the encoded token.
    /// See <c>RFC 7515</c> Appendix F for more information.
    /// </summary>
    public bool DetachPayload { get; init; }

    /// <summary>
    /// Gets the options that control JSON serialization behavior when encoding a strongly-typed payload.
    /// The default value is <c>null</c>.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; init; }

    /// <summary>
    /// Gets the collection of signatures to produce. At least one signature is required; the
    /// <see cref="JoseSerializationFormat.Compact"/> and <see cref="JoseSerializationFormat.Flattened"/> formats
    /// require exactly one.
    /// </summary>
    public required IReadOnlyCollection<JoseSignatureOptions> Signatures { get; init; }
}

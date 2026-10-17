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
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose;

/// <summary>
/// Contains the options that are required when encoding a JWE token, for any serialization format.
/// The content-encryption (<c>enc</c>) and compression (<c>zip</c>) algorithms are shared by all recipients and
/// are specified here; each recipient wraps the shared content-encryption key (CEK) with its own key-management
/// algorithm. The compact serialization is the degenerate case of exactly one recipient with no shared unprotected
/// header and no additional authenticated data (AAD); the flattened and general JSON serializations additionally
/// support those, and the general JSON serialization supports multiple recipients.
/// </summary>
[PublicAPI]
public sealed class JoseEncryptionOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncryptionOptions"/> class. The <see cref="Recipients"/>
    /// property must be set via an object initializer.
    /// </summary>
    /// <param name="encryptionAlgorithm">The shared content-encryption (<c>enc</c>) algorithm.</param>
    public JoseEncryptionOptions(AuthenticatedEncryptionAlgorithm encryptionAlgorithm)
    {
        EncryptionAlgorithm = encryptionAlgorithm;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncryptionOptions"/> class with a single recipient using
    /// the specified aggregate credentials and default options.
    /// </summary>
    /// <param name="encryptionCredentials">The aggregate credentials (KEK, key-management algorithm,
    /// content-encryption algorithm, and optional compression algorithm) used to encrypt to a single recipient.</param>
    [SetsRequiredMembers]
    public JoseEncryptionOptions(JoseEncryptionCredentials encryptionCredentials)
    {
        EncryptionAlgorithm = encryptionCredentials.AuthenticatedEncryptionAlgorithm;
        CompressionAlgorithm = encryptionCredentials.CompressionAlgorithm;
        Recipients =
        [
            new JoseRecipientOptions(
                new JoseKeyManagementCredentials(
                    encryptionCredentials.SecretKey,
                    encryptionCredentials.KeyManagementAlgorithm
                )
            ),
        ];
    }

    /// <summary>
    /// Gets the serialization format to produce. The default value is <see cref="JoseSerializationFormat.Compact"/>.
    /// The <see cref="JoseSerializationFormat.Compact"/> and <see cref="JoseSerializationFormat.Flattened"/> formats
    /// require exactly one recipient.
    /// </summary>
    public JoseSerializationFormat Format { get; init; } = JoseSerializationFormat.Compact;

    /// <summary>
    /// Gets the shared <see cref="AuthenticatedEncryptionAlgorithm"/> (<c>enc</c>) used to encrypt the payload once
    /// for all recipients.
    /// </summary>
    public AuthenticatedEncryptionAlgorithm EncryptionAlgorithm { get; }

    /// <summary>
    /// Gets the optional shared <see cref="CompressionAlgorithm"/> (<c>zip</c>) applied to the payload before
    /// encryption. The default value is <c>null</c> (no compression).
    /// </summary>
    public CompressionAlgorithm? CompressionAlgorithm { get; init; }

    /// <summary>
    /// Gets the value for the <c>typ</c> JOSE header parameter that is emitted into the shared protected header.
    /// The default value is <c>JWT</c>. Use <c>null</c> or an empty string to omit the header.
    /// </summary>
    public string? TokenType { get; init; } = JoseTokenTypes.Jwt;

    /// <summary>
    /// Gets any additional members to include in the shared JOSE Protected Header (the integrity-protected header
    /// shared by all recipients). For the compact serialization, the single recipient's key-management headers are
    /// folded into this header. The default value is <c>null</c>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? ProtectedHeaders { get; init; }

    /// <summary>
    /// Gets any additional members to include in the shared JWE Shared Unprotected Header (the <c>unprotected</c>
    /// member shared by all recipients but not integrity protected). This is only valid for the JSON serialization.
    /// The default value is <c>null</c>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? SharedUnprotectedHeaders { get; init; }

    /// <summary>
    /// Gets the optional additional authenticated data (AAD) that is integrity protected but not encrypted.
    /// This is only valid for the JSON serialization. The default value is <c>null</c>.
    /// </summary>
    public ReadOnlyMemory<byte>? AdditionalAuthenticatedData { get; init; }

    /// <summary>
    /// Gets the options that control JSON serialization behavior when encoding a strongly-typed payload.
    /// The default value is <c>null</c>.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; init; }

    /// <summary>
    /// Gets the collection of recipients to produce. At least one recipient is required; the
    /// <see cref="JoseSerializationFormat.Compact"/> and <see cref="JoseSerializationFormat.Flattened"/> formats
    /// require exactly one. Direct key-management algorithms (<c>dir</c> and <c>ECDH-ES</c>) may only be used with
    /// a single recipient.
    /// </summary>
    public required IReadOnlyCollection<JoseRecipientOptions> Recipients { get; init; }
}

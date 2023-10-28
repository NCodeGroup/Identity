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

namespace NIdentity.OpenId.DataContracts;

// TODO: add versioning
// TODO: add data protection

/// <summary>
/// Contains the configuration for a cryptographic secret.
/// </summary>
public class Secret : ISupportId
{
    /// <summary>
    /// Gets or sets the surrogate key for this entity.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for this entity.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// Valid values are defined in RFC 7517 Section 4.2:
    /// https://tools.ietf.org/html/rfc7517#section-4.2
    /// </summary>
    public string? Use { get; set; }

    /// <summary>
    /// Gets or sets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when this secret was created.
    /// </summary>
    public DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when this secret expires and is no longer valid.
    /// </summary>
    public DateTimeOffset ExpiresWhen { get; set; }

    /// <summary>
    /// Gets or sets a value that specifies the type of secret.
    /// See <see cref="SecretConstants.SecretTypes"/> for possible values.
    /// </summary>
    public string SecretType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value that specifies how <see cref="EncodedValue"/> is encoded to/from a string.
    /// See <see cref="SecretConstants.EncodingTypes"/> for possible values.
    /// </summary>
    public string EncodingType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the encoded value of the secret.
    /// </summary>
    public string EncodedValue { get; set; } = string.Empty;
}

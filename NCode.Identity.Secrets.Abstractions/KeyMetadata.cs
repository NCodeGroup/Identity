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

namespace NCode.Identity.Secrets;

/// <summary>
/// Contains common metadata for a secret key such as <c>TenantId</c>, <c>KeyId</c>, <c>Use</c>, and <c>Algorithm</c>.
/// </summary>
[PublicAPI]
public readonly struct KeyMetadata
{
    /// <summary>
    /// Gets or sets the <c>Key ID (KID)</c> for the secret key.
    /// </summary>
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets or sets the intended use for the secret key. This value is optional and may be <c>null</c> to
    /// indicate that this key is intended for use with any compatible algorithm.
    /// Valid values are defined in RFC 7517 Section 4.2:
    /// https://tools.ietf.org/html/rfc7517#section-4.2
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for the secret key. This value is optional and may be
    /// <c>null</c> to indicate that this key is intended for use with any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when set secret key expires and is no longer valid.
    /// This value is optional and may be <c>null</c> to indicate that this key never expires.
    /// </summary>
    public DateTimeOffset? ExpiresWhen { get; init; }
}

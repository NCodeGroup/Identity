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

using System.ComponentModel.DataAnnotations;

namespace NCode.Identity.OpenId.Data.Contracts;

/// <summary>
/// Represents a persisted <c>Authorization Code</c> grant for an <c>OAuth</c> or <c>OpenID Connect</c> authorization request.
/// </summary>
public class AuthorizationCode : ISupportId
{
    /// <summary>
    /// Gets or sets the surrogate key for this entity.
    /// </summary>
    public required long Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the natural key that uniquely identifies this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string HashedCode { get; set; }

    /// <summary>
    /// Gets or sets when the authorization code was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets when the authorization code expires.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the original authorization request.
    /// </summary>
    public required string AuthorizationRequestJson { get; set; }
}

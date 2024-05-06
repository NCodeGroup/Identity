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
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> grant.
/// </summary>
public class PersistedGrant : ISupportId, ISupportTenantId
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    public required long Id { get; set; }

    /// <summary>
    /// Gets or sets the natural tenant identifier for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the type of grant.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string GrantType { get; set; }

    /// <summary>
    /// Gets or sets the <c>SHA-256</c> hash of the natural key that uniquely identifies this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string HashedKey { get; set; }

    /// <summary>
    /// Gets or sets the <c>ClientId</c> associated with this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the <c>SubjectId</c> associated with this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string? SubjectId { get; set; }

    /// <summary>
    /// Gets or sets when this entity was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets when this entity expires.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; set; }

    /// <summary>
    /// Gets or sets when this entity was consumed.
    /// </summary>
    public required DateTimeOffset? ConsumedWhen { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the payload.
    /// </summary>
    public required string PayloadJson { get; set; }
}

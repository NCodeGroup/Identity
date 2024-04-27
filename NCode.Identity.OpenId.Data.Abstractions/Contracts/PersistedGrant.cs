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
/// Contains the details for a grant that is persisted to storage.
/// </summary>
public class PersistedGrant : ISupportId
{
    /// <summary>
    /// Gets or sets the surrogate key for this entity.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of grant.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the natural key that uniquely identifies this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string HashedKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <c>ClientId</c> associated with this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the <c>SubjectId</c> associated with this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string? SubjectId { get; set; }

    /// <summary>
    /// Gets or sets when this entity was created.
    /// </summary>
    public DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets when this entity expires.
    /// </summary>
    public DateTimeOffset ExpiresWhen { get; set; }

    /// <summary>
    /// Gets or sets when this entity was consumed.
    /// </summary>
    public DateTimeOffset? ConsumedWhen { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the payload.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;
}

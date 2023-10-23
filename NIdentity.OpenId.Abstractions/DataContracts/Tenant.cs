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

namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Contains the configuration for an <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// </summary>
public class Tenant : ISupportId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate key for this entity.
    /// </summary>
    public long Id { get; set; }

    /// <inheritdoc/>
    [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this entity.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }
}

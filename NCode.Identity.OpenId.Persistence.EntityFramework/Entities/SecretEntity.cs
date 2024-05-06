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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

internal class SecretEntity : ISupportId, ISupportTenant, ISupportConcurrencyToken
{
    public required long Id { get; set; }

    public required long TenantId { get; set; }

    [MaxLength(DataConstants.MaxIndexLength)]
    public required string SecretId { get; set; }

    /// <summary>
    /// Gets or sets the value of <see cref="SecretId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public required string NormalizedSecretId { get; set; }

    #region Data Properties

    [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
    public required string ConcurrencyToken { get; set; }

    public required string? Use { get; set; }

    public required string? Algorithm { get; set; }

    public required DateTimeOffset CreatedWhen { get; set; }

    public required DateTimeOffset ExpiresWhen { get; set; }

    public required string SecretType { get; set; }

    public required int KeySizeBits { get; set; }

    public required int UnprotectedSizeBytes { get; set; }

    public required string ProtectedValue { get; set; }

    #endregion

    #region Navigation Properties

    public required TenantEntity Tenant { get; set; }

    #endregion
}

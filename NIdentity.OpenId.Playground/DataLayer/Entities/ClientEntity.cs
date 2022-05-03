#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Playground.DataLayer.Entities;

internal readonly struct PropertyDescriptor
{
    public PropertyDescriptor(
        string codeName,
        string displayName,
        string description,
        Type valueType,
        object? defaultValue,
        bool isRequired)
    {
        CodeName = codeName;
        DisplayName = displayName;
        Description = description;
        ValueType = valueType;
        DefaultValue = defaultValue;
        IsRequired = isRequired;
    }

    public string CodeName { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public Type ValueType { get; }

    public object? DefaultValue { get; }

    public bool IsRequired { get; }
}

internal class ClientEntity : ISupportId, ISupportConcurrencyToken
{
    public long Id { get; set; }

    [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
    public string ConcurrencyToken { get; set; } = null!;

    [MaxLength(DataConstants.MaxIndexLength)]
    public string ClientId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the value of <see cref="ClientId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [MaxLength(DataConstants.MaxIndexLength)]
    public string NormalizedClientId { get; set; } = null!;

    public bool IsDisabled { get; set; }

    public bool AllowUnsafeTokenResponse { get; set; }

    public bool AllowLoopback { get; set; }

    public bool RequireRequestObject { get; set; }

    public bool RequirePkce { get; set; }

    public bool AllowPlainCodeChallengeMethod { get; set; }

    public IList<ClientSecretEntity> ClientSecrets { get; set; } = null!;

    public IList<ClientUrlEntity> Urls { get; set; } = null!;

    public IList<ClientPropertyEntity> Properties { get; set; } = null!;
}
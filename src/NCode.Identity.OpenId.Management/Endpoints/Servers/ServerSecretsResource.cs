#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedServerSecrets"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class ServerSecretsResource : ISupportServerId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="PersistedServerSecrets.ServerId"/>
    public required string ServerId { get; init; }

    /// <inheritdoc cref="PersistedServerSecrets.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to an OpenID Server instance.
    /// </summary>
    public required IReadOnlyCollection<SecretResource> Secrets { get; init; }
}

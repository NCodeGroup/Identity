#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> Server instance.
/// </summary>
[PublicAPI]
public class PersistedServer : BasePersistedServerResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    /// <summary>
    /// Gets or sets the JSON settings for an OpenID Server instance.
    /// </summary>
    public required PersistedServerSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to an OpenID Server instance.
    /// </summary>
    public required PersistedServerSecrets Secrets { get; set; }
}

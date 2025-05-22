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

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the collection of secrets only known to an OpenID Client instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class PersistedClientSecrets : PersistedClientResource<IReadOnlyCollection<PersistedSecret>>, ISupportPersistedSecretCollection
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => $"{ResourceTypePrefix}{ResourceTypes.Separator}{SecretResourceTypes.Secrets}";

    /// <inheritdoc/>
    IReadOnlyCollection<PersistedSecret> ISupportPersistedSecretCollection.Secrets => Value;
}

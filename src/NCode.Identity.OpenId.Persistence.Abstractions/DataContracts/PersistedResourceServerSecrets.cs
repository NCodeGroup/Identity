// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the collection of secrets owned by an OpenID resource server (an API, in Auth0 terms).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedResourceServerSecrets
    : PersistedResourceServerResource<IReadOnlyCollection<PersistedSecret>>,
        ISupportPersistedSecretCollection
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType =>
        $"{ResourceTypePrefix}{ResourceTypes.Separator}{SecretResourceTypes.Secrets}";

    /// <inheritdoc/>
    IReadOnlyCollection<PersistedSecret> ISupportPersistedSecretCollection.Secrets => Value;
}

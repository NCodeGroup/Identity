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
/// Contains a single secret owned by an OpenID resource server (an API, in Auth0 terms), resolved together with its
/// owning tenant so that the caller can scope authorization without loading the whole secret collection.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedResourceServerSecret : PersistedResourceServerResource<PersistedSecret>
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType =>
        $"{ResourceTypePrefix}{ResourceTypes.Separator}{SecretResourceTypes.Secret}";
}

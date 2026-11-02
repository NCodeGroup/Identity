// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the JSON settings for an OpenID resource server (an API, in Auth0 terms).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedResourceServerSettings : PersistedResourceServerResource<JsonElement>
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType =>
        $"{ResourceTypePrefix}{ResourceTypes.Separator}{OpenIdResourceTypes.Settings}";
}

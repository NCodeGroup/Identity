// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the JSON settings for an OpenID Team instance.
/// </summary>
// See ADR-1403
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedTeamSettings : PersistedTeamResource<JsonElement>
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType =>
        $"{ResourceTypePrefix}{ResourceTypes.Separator}{OpenIdResourceTypes.Settings}";
}

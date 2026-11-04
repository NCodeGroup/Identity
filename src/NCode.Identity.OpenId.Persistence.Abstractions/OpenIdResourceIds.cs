// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Contains the well-known resource identifiers of the family.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class OpenIdResourceIds
{
    /// <summary>
    /// Contains the identifier of the platform, the one deployment root: its settings document, its authority node
    /// and its master-key owner all carry it.
    /// </summary>
    public const string Platform = "platform";
}

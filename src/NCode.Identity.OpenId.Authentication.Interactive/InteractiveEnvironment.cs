// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The non-production environment a tenant's pages are served for, which the host embeds in the branding so the
/// single-page bundle can show that the page is not a production one. A production tenant has none.
/// </summary>
/// <param name="Type">The name of the tenant's effective environment type, such as <c>staging</c>.</param>
/// <param name="Name">The tenant's own name for its environment, or <see langword="null"/> when it has none.</param>
// See ADR-1289.
internal sealed record InteractiveEnvironment(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name
);

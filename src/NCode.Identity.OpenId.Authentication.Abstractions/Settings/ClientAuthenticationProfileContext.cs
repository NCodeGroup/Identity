// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// The context supplied to an <see cref="IClientAuthenticationProfileRequirement"/> after a client has authenticated
/// with a signed assertion.
/// </summary>
/// <param name="OpenIdContext">The ambient <see cref="OpenIdContext"/> (environment, error factory, tenant, services).</param>
/// <param name="Client">The client that presented the assertion.</param>
/// <param name="AssertionPayload">The validated <c>client_assertion</c> JWT payload.</param>
[PublicAPI]
public sealed record ClientAuthenticationProfileContext(
    OpenIdContext OpenIdContext,
    OpenIdClient Client,
    JsonElement AssertionPayload
);

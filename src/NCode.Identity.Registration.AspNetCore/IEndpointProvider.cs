// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Routing;

namespace NCode.Identity.Registration.AspNetCore;

/// <summary>
/// Provides the ability for providers to map endpoints onto the HTTP request pipeline.
/// </summary>
[PublicAPI]
public interface IEndpointProvider
{
    /// <summary>
    /// Adds a <see cref="RouteEndpoint"/> to the <see cref="IEndpointRouteBuilder"/> for a specific endpoint(s).
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> instance.</param>
    void Map(IEndpointRouteBuilder endpoints);
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A startup filter that adds the management problem response middleware as the outermost middleware of the host, so
/// it also sees the answers of the router and of the host's own middleware.
/// </summary>
// See ADR-1483.
internal class ManagementProblemResponseStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<ManagementProblemResponseMiddleware>();
            next(app);
        };
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Makes a circuit's connection session current for the circuit's inbound activity, so the circuit's
/// <c>AddBffAccessTokenHandler</c> clients obtain tokens without an <c>HttpContext</c>.
/// </summary>
/// <param name="authenticationStateProvider">The circuit's authentication state provider.</param>
/// <param name="connections">The accessor that holds the connection session that is current.</param>
internal sealed class BffCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    IBffConnectionSessionAccessor connections
) : CircuitHandler
{
    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next
    )
    {
        ArgumentNullException.ThrowIfNull(next);

        return async context =>
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            if (state.User.GetBffConnectionSession() is not { } connection)
            {
                await next(context);
                return;
            }

            using (connections.Use(connection))
            {
                await next(context);
            }
        };
    }
}

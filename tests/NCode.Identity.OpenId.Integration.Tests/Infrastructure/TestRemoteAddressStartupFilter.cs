// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// An <see cref="IStartupFilter"/> that sets each request's remote address from the <see cref="HeaderName"/> header
/// before the host's pipeline runs, because <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> gives a request no
/// remote address. A test that needs one, such as a limit counted per address, sends the header.
/// </summary>
public sealed class TestRemoteAddressStartupFilter : IStartupFilter
{
    /// <summary>
    /// The header the filter reads the remote address from.
    /// </summary>
    public const string HeaderName = "X-Test-Remote-Address";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(
                (context, nextMiddleware) =>
                {
                    if (
                        context.Request.Headers.TryGetValue(HeaderName, out var value)
                        && IPAddress.TryParse(value.ToString(), out var address)
                    )
                        context.Connection.RemoteIpAddress = address;

                    return nextMiddleware(context);
                }
            );
            next(app);
        };
}

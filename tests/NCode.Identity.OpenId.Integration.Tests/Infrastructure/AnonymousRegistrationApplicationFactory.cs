// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that allows anonymous dynamic client registration (RFC 7591) through the
/// platform's configured <c>registration_endpoint_allow_anonymous</c> setting, so a test can register a client without
/// first minting an initial access token.
/// </summary>
public sealed class AnonymousRegistrationApplicationFactory : HostApplicationFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenId:Platform:Settings:registration_endpoint_allow_anonymous"] = "true",
                }
            )
        );
    }
}

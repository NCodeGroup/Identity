// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Claims;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Serialization;

public class ClaimJsonConverterTests
{
    private ServiceProvider ServiceProvider { get; }
    private JsonSerializerOptions Options { get; }

    public ClaimJsonConverterTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddClaimsLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        var serializer = ServiceProvider.GetRequiredService<IClaimsSerializer>();
        Options = new JsonSerializerOptions
        {
            Converters =
            {
                new ClaimJsonConverter(serializer),
                new ClaimsIdentityJsonConverter(serializer),
                new ClaimsPrincipalJsonConverter(serializer),
            },
        };
    }

    #region Claim Tests

    [Fact]
    public void Claim_RoundTrips()
    {
        var claim = new Claim("name", "alice", "vt", "issuer", "original");

        var json = JsonSerializer.Serialize(claim, Options);
        var result = JsonSerializer.Deserialize<Claim>(json, Options);

        Assert.NotNull(result);
        Assert.Equal("name", result.Type);
        Assert.Equal("alice", result.Value);
        Assert.Equal("issuer", result.Issuer);
    }

    #endregion

    #region ClaimsIdentity Tests

    [Fact]
    public void ClaimsIdentity_RoundTrips()
    {
        var identity = new ClaimsIdentity("pwd", "name", "role");
        identity.AddClaim(new Claim("name", "alice"));

        var json = JsonSerializer.Serialize(identity, Options);
        var result = JsonSerializer.Deserialize<ClaimsIdentity>(json, Options);

        Assert.NotNull(result);
        Assert.Equal("pwd", result.AuthenticationType);
        Assert.Equal("alice", result.FindFirst("name")?.Value);
    }

    #endregion

    #region ClaimsPrincipal Tests

    [Fact]
    public void ClaimsPrincipal_RoundTrips()
    {
        var identity = new ClaimsIdentity("pwd");
        identity.AddClaim(new Claim("name", "alice"));
        var principal = new ClaimsPrincipal(identity);

        var json = JsonSerializer.Serialize(principal, Options);
        var result = JsonSerializer.Deserialize<ClaimsPrincipal>(json, Options);

        Assert.NotNull(result);
        var resultIdentity = Assert.Single(result.Identities);
        Assert.Equal("alice", resultIdentity.FindFirst("name")?.Value);
    }

    #endregion
}

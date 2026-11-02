// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Claims;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Serialization;

public class ClaimJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public ClaimJsonConverterTests()
    {
        var serializer = DefaultClaimsSerializer.Singleton;
        _options = new JsonSerializerOptions
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

        var json = JsonSerializer.Serialize(claim, _options);
        var result = JsonSerializer.Deserialize<Claim>(json, _options);

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

        var json = JsonSerializer.Serialize(identity, _options);
        var result = JsonSerializer.Deserialize<ClaimsIdentity>(json, _options);

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

        var json = JsonSerializer.Serialize(principal, _options);
        var result = JsonSerializer.Deserialize<ClaimsPrincipal>(json, _options);

        Assert.NotNull(result);
        var resultIdentity = Assert.Single(result.Identities);
        Assert.Equal("alice", resultIdentity.FindFirst("name")?.Value);
    }

    #endregion
}

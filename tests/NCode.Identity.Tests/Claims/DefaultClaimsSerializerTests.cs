// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Claims;

public class DefaultClaimsSerializerTests
{
    private DefaultClaimsSerializer Serializer { get; } = DefaultClaimsSerializer.Singleton;

    #region SerializeClaim Tests

    [Fact]
    public void SerializeClaim_MapsAllFields()
    {
        var claim = new Claim("name", "alice", "vt", "issuer", "original");
        claim.Properties["k"] = "v";

        var serializable = Serializer.SerializeClaim(claim);

        Assert.Equal("name", serializable.Type);
        Assert.Equal("alice", serializable.Value);
        Assert.Equal("vt", serializable.ValueType);
        Assert.Equal("issuer", serializable.Issuer);
        Assert.Equal("original", serializable.OriginalIssuer);
        Assert.Equal("v", serializable.Properties["k"]);
        Assert.Null(serializable.SubjectRef);
    }

    #endregion

    #region Claim Round-Trip Tests

    [Fact]
    public void DeserializeClaim_RoundTripsFieldsAndProperties()
    {
        var claim = new Claim("name", "alice", "vt", "issuer", "original");
        claim.Properties["k"] = "v";
        var serializable = Serializer.SerializeClaim(claim);

        var result = Serializer.DeserializeClaim(serializable);

        Assert.Equal("name", result.Type);
        Assert.Equal("alice", result.Value);
        Assert.Equal("vt", result.ValueType);
        Assert.Equal("issuer", result.Issuer);
        Assert.Equal("original", result.OriginalIssuer);
        Assert.Equal("v", result.Properties["k"]);
    }

    #endregion

    #region Identity Round-Trip Tests

    [Fact]
    public void SerializeAndDeserializeIdentity_RoundTrips()
    {
        var identity = new ClaimsIdentity("pwd", "name", "role") { Label = "label" };
        identity.AddClaim(new Claim("name", "alice"));
        identity.AddClaim(new Claim("role", "admin"));

        var serializable = Serializer.SerializeIdentity(identity);
        var result = Serializer.DeserializeIdentity(serializable);

        Assert.Equal("pwd", result.AuthenticationType);
        Assert.Equal("name", result.NameClaimType);
        Assert.Equal("role", result.RoleClaimType);
        Assert.Equal("label", result.Label);
        Assert.Equal("alice", result.FindFirst("name")?.Value);
        Assert.Equal("admin", result.FindFirst("role")?.Value);
    }

    [Fact]
    public void SerializeIdentity_PreservesSubjectReferenceForOwnedClaims()
    {
        var identity = new ClaimsIdentity("pwd");
        identity.AddClaim(new Claim("name", "alice"));

        var serializable = Serializer.SerializeIdentity(identity);

        var claim = Assert.Single(serializable.Claims);
        Assert.Equal(serializable.ReferenceId, claim.SubjectRef);

        var result = Serializer.DeserializeIdentity(serializable);
        Assert.Same(result, result.FindFirst("name")?.Subject);
    }

    [Fact]
    public void SerializeIdentity_WithActor_RoundTripsActor()
    {
        var actor = new ClaimsIdentity("actor-auth");
        actor.AddClaim(new Claim("actor", "bob"));
        var identity = new ClaimsIdentity("pwd") { Actor = actor };
        identity.AddClaim(new Claim("name", "alice"));

        var serializable = Serializer.SerializeIdentity(identity);
        var result = Serializer.DeserializeIdentity(serializable);

        Assert.NotNull(result.Actor);
        Assert.Equal("actor-auth", result.Actor.AuthenticationType);
        Assert.Equal("bob", result.Actor.FindFirst("actor")?.Value);
    }

    #endregion

    #region Principal Round-Trip Tests

    [Fact]
    public void SerializeAndDeserializePrincipal_RoundTrips()
    {
        var identity = new ClaimsIdentity("pwd");
        identity.AddClaim(new Claim("name", "alice"));
        var principal = new ClaimsPrincipal(identity);

        var serializable = Serializer.SerializePrincipal(principal);
        var result = Serializer.DeserializePrincipal(serializable);

        var resultIdentity = Assert.Single(result.Identities);
        Assert.Equal("alice", resultIdentity.FindFirst("name")?.Value);
    }

    #endregion
}

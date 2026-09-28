#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;

namespace NCode.Identity.Claims;

public class SerializableClaimTests
{
    #region Property Tests

    [Fact]
    public void Properties_WhenSet_RoundTrip()
    {
        var properties = new Dictionary<string, string> { ["key"] = "value" };

        var claim = new SerializableClaim
        {
            Type = "name",
            Value = "alice",
            ValueType = "http://www.w3.org/2001/XMLSchema#string",
            Issuer = "issuer",
            OriginalIssuer = "original",
            Properties = properties,
            SubjectRef = "subject-1",
        };

        Assert.Equal("name", claim.Type);
        Assert.Equal("alice", claim.Value);
        Assert.Equal("http://www.w3.org/2001/XMLSchema#string", claim.ValueType);
        Assert.Equal("issuer", claim.Issuer);
        Assert.Equal("original", claim.OriginalIssuer);
        Assert.Same(properties, claim.Properties);
        Assert.Equal("subject-1", claim.SubjectRef);
    }

    [Fact]
    public void SubjectRef_WhenNotSet_IsNull()
    {
        var claim = new SerializableClaim
        {
            Type = "name",
            Value = "alice",
            ValueType = null,
            Issuer = null,
            OriginalIssuer = null,
            Properties = new Dictionary<string, string>(),
        };

        Assert.Null(claim.SubjectRef);
    }

    #endregion
}

public class SerializableClaimsIdentityTests
{
    #region Property Tests

    [Fact]
    public void Properties_WhenSet_RoundTrip()
    {
        using var document = JsonDocument.Parse("{}");
        var bootstrap = document.RootElement.Clone();
        var claims = new List<SerializableClaim>();

        var identity = new SerializableClaimsIdentity
        {
            ReferenceId = "ref-1",
            Label = "label",
            BootstrapContext = bootstrap,
            AuthenticationType = "pwd",
            NameClaimType = "name",
            RoleClaimType = "role",
            Actor = null,
            Claims = claims,
        };

        Assert.Equal("ref-1", identity.ReferenceId);
        Assert.Equal("label", identity.Label);
        Assert.Equal("pwd", identity.AuthenticationType);
        Assert.Equal("name", identity.NameClaimType);
        Assert.Equal("role", identity.RoleClaimType);
        Assert.Null(identity.Actor);
        Assert.Same(claims, identity.Claims);
    }

    [Fact]
    public void Actor_WhenSet_RoundTrips()
    {
        using var document = JsonDocument.Parse("{}");
        var bootstrap = document.RootElement.Clone();

        var actor = new SerializableClaimsIdentity
        {
            ReferenceId = "actor-ref",
            Label = null,
            BootstrapContext = bootstrap,
            AuthenticationType = null,
            NameClaimType = null,
            RoleClaimType = null,
            Actor = null,
            Claims = [],
        };

        var identity = new SerializableClaimsIdentity
        {
            ReferenceId = "ref-1",
            Label = null,
            BootstrapContext = bootstrap,
            AuthenticationType = null,
            NameClaimType = null,
            RoleClaimType = null,
            Actor = actor,
            Claims = [],
        };

        Assert.Same(actor, identity.Actor);
    }

    #endregion
}

public class SerializableClaimsPrincipalTests
{
    #region Property Tests

    [Fact]
    public void Identities_WhenSet_RoundTrips()
    {
        var identities = new List<SerializableClaimsIdentity>();

        var principal = new SerializableClaimsPrincipal { Identities = identities };

        Assert.Same(identities, principal.Identities);
    }

    #endregion
}

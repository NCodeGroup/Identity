// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Principals;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

public class SubjectMetadataExtensionsTests
{
    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity("scheme");
        identity.AddClaims(claims);
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void TryGetMetadata_WhenPresent_ReturnsParsedObject()
    {
        var principal = CreatePrincipal(
            new Claim(SubjectMetadataClaimTypes.ProfileMetadata, """{"theme":"dark"}""")
        );

        var found = principal.TryGetMetadata(
            SubjectMetadataClaimTypes.ProfileMetadata,
            out var metadata
        );

        Assert.True(found);
        Assert.Equal(JsonValueKind.Object, metadata.ValueKind);
        Assert.Equal("dark", metadata.GetProperty("theme").GetString());
    }

    [Fact]
    public void TryGetMetadata_WhenAbsent_ReturnsFalseAndEmptyObject()
    {
        var principal = CreatePrincipal();

        var found = principal.TryGetMetadata(
            SubjectMetadataClaimTypes.SystemMetadata,
            out var metadata
        );

        Assert.False(found);
        Assert.Equal(JsonValueKind.Object, metadata.ValueKind);
        Assert.False(metadata.EnumerateObject().MoveNext());
    }

    [Fact]
    public void TryGetMetadata_WhenMalformed_ReturnsFalseAndEmptyObject()
    {
        var principal = CreatePrincipal(
            new Claim(SubjectMetadataClaimTypes.SystemMetadata, "{not-json")
        );

        var found = principal.TryGetMetadata(
            SubjectMetadataClaimTypes.SystemMetadata,
            out var metadata
        );

        Assert.False(found);
        Assert.Equal(JsonValueKind.Object, metadata.ValueKind);
    }
}

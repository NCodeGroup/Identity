#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.OpenId.Accounts;
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
            new Claim(AccountConstants.ProfileMetadataClaimType, """{"theme":"dark"}""")
        );

        var found = principal.TryGetMetadata(
            AccountConstants.ProfileMetadataClaimType,
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
            AccountConstants.SystemMetadataClaimType,
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
            new Claim(AccountConstants.SystemMetadataClaimType, "{not-json")
        );

        var found = principal.TryGetMetadata(
            AccountConstants.SystemMetadataClaimType,
            out var metadata
        );

        Assert.False(found);
        Assert.Equal(JsonValueKind.Object, metadata.ValueKind);
    }
}

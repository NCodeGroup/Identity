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

using System.Text.Json;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class FrontendBootstrapDocumentTests
{
    #region Create Tests

    [Fact]
    public void Create_WhenApiBaseUrlAndIssuerUnset_FallsBackToRequestOrigin()
    {
        var options = new FrontendBootstrapOptions();

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.Equal("https://id.example", document.ApiBaseUrl);
        Assert.Equal("https://id.example", document.Issuer);
    }

    [Fact]
    public void Create_WhenConfigured_PrefersConfiguredValuesOverRequestOrigin()
    {
        var options = new FrontendBootstrapOptions
        {
            ApiBaseUrl = "https://api.example",
            Issuer = "https://issuer.example",
            ClientId = "spa",
        };

        var document = FrontendBootstrapDocument.Create(options, "https://serving.example");

        Assert.Equal("https://api.example", document.ApiBaseUrl);
        Assert.Equal("https://issuer.example", document.Issuer);
        Assert.Equal("spa", document.ClientId);
    }

    [Fact]
    public void Create_NormalizesTrailingSlashOnOrigins()
    {
        var options = new FrontendBootstrapOptions { ApiBaseUrl = "https://api.example/" };

        var document = FrontendBootstrapDocument.Create(options, "https://serving.example/");

        Assert.Equal("https://api.example", document.ApiBaseUrl);
        Assert.Equal("https://serving.example", document.Issuer);
    }

    [Fact]
    public void Create_WhenScopesEmpty_OmitsScopes()
    {
        var options = new FrontendBootstrapOptions { Scopes = [] };

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.Null(document.Scopes);
    }

    [Fact]
    public void Create_WhenScopesConfigured_CopiesScopes()
    {
        var options = new FrontendBootstrapOptions { Scopes = ["openid", "read:clients"] };

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.Equal(["openid", "read:clients"], document.Scopes);
    }

    [Fact]
    public void Create_WhenBrandingEmpty_OmitsBranding()
    {
        var options = new FrontendBootstrapOptions { Branding = new FrontendBrandingOptions() };

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.Null(document.Branding);
    }

    [Fact]
    public void Create_WhenBrandingPresent_CopiesBranding()
    {
        var options = new FrontendBootstrapOptions
        {
            Branding = new FrontendBrandingOptions { DisplayName = "Acme", Mode = "dark" },
        };

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.NotNull(document.Branding);
        Assert.Equal("Acme", document.Branding.DisplayName);
        Assert.Equal("dark", document.Branding.Mode);
    }

    [Fact]
    public void Create_CopiesFeatures()
    {
        var options = new FrontendBootstrapOptions { Features = { ["dcr"] = true } };

        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        Assert.True(document.Features["dcr"]);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void Serialize_ProducesCamelCaseShape_AndOmitsAbsentMembers()
    {
        var options = new FrontendBootstrapOptions
        {
            ClientId = "spa",
            Features = { ["dcr"] = true },
            Branding = new FrontendBrandingOptions { DisplayName = "Acme", Mode = "light" },
        };
        var document = FrontendBootstrapDocument.Create(options, "https://id.example");

        var json = JsonSerializer.Serialize(document, JsonSerializerOptions.Web);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.Equal("https://id.example", root.GetProperty("apiBaseUrl").GetString());
        Assert.Equal("https://id.example", root.GetProperty("issuer").GetString());
        Assert.Equal("spa", root.GetProperty("clientId").GetString());
        Assert.True(root.GetProperty("features").GetProperty("dcr").GetBoolean());
        Assert.Equal("Acme", root.GetProperty("branding").GetProperty("displayName").GetString());
        Assert.Equal("light", root.GetProperty("branding").GetProperty("mode").GetString());
        Assert.False(root.TryGetProperty("scopes", out _));
    }

    [Fact]
    public void Serialize_WhenMinimal_OmitsOptionalMembersButKeepsFeatures()
    {
        var document = FrontendBootstrapDocument.Create(
            new FrontendBootstrapOptions(),
            "https://id.example"
        );

        var json = JsonSerializer.Serialize(document, JsonSerializerOptions.Web);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.False(root.TryGetProperty("clientId", out _));
        Assert.False(root.TryGetProperty("scopes", out _));
        Assert.False(root.TryGetProperty("branding", out _));
        Assert.Equal(JsonValueKind.Object, root.GetProperty("features").ValueKind);
    }

    #endregion
}

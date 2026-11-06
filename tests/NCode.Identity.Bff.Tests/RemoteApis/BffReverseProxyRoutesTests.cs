// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.RemoteApis;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffReverseProxyRoutesTests
{
    private static readonly HashSet<string> Requirements =
    [
        BffTokenRequirementNames.User,
        BffTokenRequirementNames.None,
    ];

    private static readonly HashSet<string> Frontends = ["default", "admin"];

    private static BffReverseProxyRouteNames Names { get; } = new(Requirements, Frontends);

    private static RouteConfig CreateRoute(Dictionary<string, string>? metadata) =>
        new()
        {
            RouteId = "orders",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/api/orders/{**rest}" },
            Metadata = metadata,
        };

    private static List<Exception> Validate(Dictionary<string, string>? metadata)
    {
        List<Exception> errors = [];
        BffReverseProxyRoutes.Validate(CreateRoute(metadata), Names, errors);
        return errors;
    }

    #region IsRemoteApi Tests

    [Fact]
    public void IsRemoteApi_WithTokenRequirement_ReturnsTrue()
    {
        var route = CreateRoute(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
            }
        );

        Assert.True(BffReverseProxyRoutes.IsRemoteApi(route));
    }

    [Fact]
    public void IsRemoteApi_OtherMetadata_ReturnsFalse()
    {
        var route = CreateRoute(new Dictionary<string, string> { ["Other"] = "value" });

        Assert.False(BffReverseProxyRoutes.IsRemoteApi(route));
    }

    [Fact]
    public void IsRemoteApi_NoMetadata_ReturnsFalse()
    {
        Assert.False(BffReverseProxyRoutes.IsRemoteApi(CreateRoute(null)));
    }

    #endregion

    #region GetTokenRequest Tests

    [Fact]
    public void GetTokenRequest_AllKeys_ReturnsTheRequest()
    {
        var route = CreateRoute(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
                [BffReverseProxyMetadataKeys.Resource] = "https://api.example.com",
                [BffReverseProxyMetadataKeys.Scope] = "orders.read",
            }
        );

        var request = BffReverseProxyRoutes.GetTokenRequest(route);

        Assert.NotNull(request);
        Assert.Equal(BffTokenRequirementNames.User, request.Requirement);
        Assert.Equal("https://api.example.com", request.Resource);
        Assert.Equal("orders.read", request.Scope);
    }

    [Fact]
    public void GetTokenRequest_RequirementOnly_ReturnsTheRequestWithoutResourceOrScope()
    {
        var route = CreateRoute(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.None,
            }
        );

        var request = BffReverseProxyRoutes.GetTokenRequest(route);

        Assert.NotNull(request);
        Assert.Equal(BffTokenRequirementNames.None, request.Requirement);
        Assert.Null(request.Resource);
        Assert.Null(request.Scope);
    }

    [Fact]
    public void GetTokenRequest_NoRequirement_ReturnsNull()
    {
        var route = CreateRoute(new Dictionary<string, string> { ["Other"] = "value" });

        Assert.Null(BffReverseProxyRoutes.GetTokenRequest(route));
    }

    [Fact]
    public void GetTokenRequest_NoMetadata_ReturnsNull()
    {
        Assert.Null(BffReverseProxyRoutes.GetTokenRequest(CreateRoute(null)));
    }

    #endregion

    #region GetFrontend Tests

    [Fact]
    public void GetFrontend_WithFrontend_ReturnsTheName()
    {
        var route = CreateRoute(
            new Dictionary<string, string> { [BffReverseProxyMetadataKeys.Frontend] = "admin" }
        );

        Assert.Equal("admin", BffReverseProxyRoutes.GetFrontend(route));
    }

    [Fact]
    public void GetFrontend_OtherMetadata_ReturnsNull()
    {
        var route = CreateRoute(new Dictionary<string, string> { ["Other"] = "value" });

        Assert.Null(BffReverseProxyRoutes.GetFrontend(route));
    }

    [Fact]
    public void GetFrontend_NoMetadata_ReturnsNull()
    {
        Assert.Null(BffReverseProxyRoutes.GetFrontend(CreateRoute(null)));
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_RegisteredRequirementWithResourceAndScope_AddsNoError()
    {
        var errors = Validate(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
                [BffReverseProxyMetadataKeys.Resource] = "https://api.example.com",
                [BffReverseProxyMetadataKeys.Scope] = "orders.read",
            }
        );

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NoMetadata_AddsNoError()
    {
        Assert.Empty(Validate(null));
    }

    [Fact]
    public void Validate_NoBffKeys_AddsNoError()
    {
        Assert.Empty(Validate(new Dictionary<string, string> { ["Other"] = "value" }));
    }

    [Fact]
    public void Validate_BlankRequirement_AddsAnError()
    {
        var errors = Validate(
            new Dictionary<string, string> { [BffReverseProxyMetadataKeys.TokenRequirement] = " " }
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffTokenRequirement must not be blank.",
            error.Message
        );
    }

    [Fact]
    public void Validate_UnregisteredRequirement_AddsAnError()
    {
        var errors = Validate(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = "unknown",
            }
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffTokenRequirement 'unknown' is not a registered token requirement.",
            error.Message
        );
    }

    [Fact]
    public void Validate_ResourceAndScopeWithoutRequirement_AddsAnErrorForEach()
    {
        var errors = Validate(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.Resource] = "https://api.example.com",
                [BffReverseProxyMetadataKeys.Scope] = "orders.read",
            }
        );

        Assert.Collection(
            errors,
            error =>
                Assert.Equal(
                    "The metadata of route 'orders' is invalid: BffResource needs BffTokenRequirement.",
                    error.Message
                ),
            error =>
                Assert.Equal(
                    "The metadata of route 'orders' is invalid: BffScope needs BffTokenRequirement.",
                    error.Message
                )
        );
    }

    [Fact]
    public void Validate_BlankResource_AddsAnError()
    {
        var errors = Validate(
            new Dictionary<string, string>
            {
                [BffReverseProxyMetadataKeys.TokenRequirement] = BffTokenRequirementNames.User,
                [BffReverseProxyMetadataKeys.Resource] = "",
            }
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffResource must not be blank.",
            error.Message
        );
    }

    [Fact]
    public void Validate_ConfiguredFrontend_AddsNoError()
    {
        var errors = Validate(
            new Dictionary<string, string> { [BffReverseProxyMetadataKeys.Frontend] = "admin" }
        );

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BlankFrontend_AddsAnError()
    {
        var errors = Validate(
            new Dictionary<string, string> { [BffReverseProxyMetadataKeys.Frontend] = " " }
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffFrontend must not be blank.",
            error.Message
        );
    }

    [Fact]
    public void Validate_UnknownFrontend_AddsAnError()
    {
        var errors = Validate(
            new Dictionary<string, string> { [BffReverseProxyMetadataKeys.Frontend] = "other" }
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The metadata of route 'orders' is invalid: BffFrontend 'other' is not a configured BFF frontend.",
            error.Message
        );
    }

    #endregion
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public class HttpBodyLoggingEndpointExtensionsTests : BaseTests
{
    private static Endpoint CreateEndpoint(params object[] metadata) =>
        new(null, new EndpointMetadataCollection(metadata), "test");

    #region HasConfidentialBodies Tests

    [Fact]
    public void HasConfidentialBodies_WhenNull_ReturnsFalse()
    {
        Endpoint? endpoint = null;

        Assert.False(endpoint.HasConfidentialBodies);
    }

    [Fact]
    public void HasConfidentialBodies_WhenMarked_ReturnsTrue()
    {
        var endpoint = CreateEndpoint(ConfidentialBodyMetadata.Instance);

        Assert.True(endpoint.HasConfidentialBodies);
    }

    #endregion

    #region LoggableBodyFields Tests

    [Fact]
    public void LoggableBodyFields_WhenNull_ReturnsNone()
    {
        Endpoint? endpoint = null;

        Assert.Equal(HttpLoggingFields.None, endpoint.LoggableBodyFields);
    }

    [Fact]
    public void LoggableBodyFields_WhenNotOptedIn_ReturnsNone()
    {
        var endpoint = CreateEndpoint();

        Assert.Equal(HttpLoggingFields.None, endpoint.LoggableBodyFields);
    }

    [Fact]
    public void LoggableBodyFields_WhenOptedInToBothBodies_ReturnsBothBodies()
    {
        var endpoint = CreateEndpoint(
            HttpBodyLoggingMetadata.RequestBody,
            HttpBodyLoggingMetadata.ResponseBody
        );

        Assert.Equal(
            HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody,
            endpoint.LoggableBodyFields
        );
    }

    [Fact]
    public void LoggableBodyFields_WhenMarkedConfidential_ReturnsNone()
    {
        var endpoint = CreateEndpoint(
            HttpBodyLoggingMetadata.RequestBody,
            HttpBodyLoggingMetadata.ResponseBody,
            ConfidentialBodyMetadata.Instance
        );

        Assert.Equal(HttpLoggingFields.None, endpoint.LoggableBodyFields);
    }

    #endregion
}

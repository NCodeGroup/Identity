// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

public sealed class TransportEndpointInspectorTests
{
    #region Inspect Tests

    [Fact]
    public void Inspect_WhenEveryEndpointIsHttps_ReturnsItAsATlsEndpoint()
    {
        var inspection = TransportEndpointInspector.Inspect([
            "https://127.0.0.1:8443",
            "https://[::]:8443",
        ]);

        Assert.Empty(inspection.Failures);
        Assert.Equal(2, inspection.TlsEndpoints.Count);
        Assert.Equal(8443, inspection.TlsEndpoints[0].Port);
        Assert.Equal(8443, inspection.TlsEndpoints[1].Port);
    }

    [Fact]
    public void Inspect_WhenAnEndpointIsPlainHttp_ReportsPlainHttp()
    {
        var inspection = TransportEndpointInspector.Inspect(["http://localhost:5000"]);

        var failure = Assert.Single(inspection.Failures);
        Assert.Equal("The server serves plain HTTP at 'http://localhost:5000'.", failure);
        Assert.Empty(inspection.TlsEndpoints);
    }

    [Fact]
    public void Inspect_WhenHttpsAndHttpAreBoth_BoundReportsOnlyTheHttpOne()
    {
        var inspection = TransportEndpointInspector.Inspect([
            "https://localhost:8443",
            "http://localhost:5000",
        ]);

        var failure = Assert.Single(inspection.Failures);
        Assert.Equal("The server serves plain HTTP at 'http://localhost:5000'.", failure);
        var endpoint = Assert.Single(inspection.TlsEndpoints);
        Assert.Equal(8443, endpoint.Port);
    }

    [Fact]
    public void Inspect_WhenNoAddressIsGiven_ReportsNoEndpoint()
    {
        var inspection = TransportEndpointInspector.Inspect([]);

        var failure = Assert.Single(inspection.Failures);
        Assert.Equal("The server binds no endpoint.", failure);
        Assert.Empty(inspection.TlsEndpoints);
    }

    [Fact]
    public void Inspect_WhenTheEndpointIsAnHttpUnixSocket_ReportsPlainHttp()
    {
        var inspection = TransportEndpointInspector.Inspect(["http://unix:/run/app.sock"]);

        var failure = Assert.Single(inspection.Failures);
        Assert.Equal("The server serves plain HTTP at 'http://unix:/run/app.sock'.", failure);
        Assert.Empty(inspection.TlsEndpoints);
    }

    [Fact]
    public void Inspect_WhenTheEndpointIsAnHttpsUnixSocket_ReportsNotATcpEndpoint()
    {
        var inspection = TransportEndpointInspector.Inspect(["https://unix:/run/app.sock"]);

        var failure = Assert.Single(inspection.Failures);
        Assert.Equal(
            "The server binds 'https://unix:/run/app.sock', which is not an HTTPS endpoint on a TCP port.",
            failure
        );
        Assert.Empty(inspection.TlsEndpoints);
    }

    #endregion
}

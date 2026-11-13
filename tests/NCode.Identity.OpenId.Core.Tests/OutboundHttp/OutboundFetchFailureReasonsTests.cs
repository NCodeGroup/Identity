// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class OutboundFetchFailureReasonsTests
{
    #region Classify Tests

    [Fact]
    public void Classify_WhenTheChainHoldsARefusal_ReturnsBlockedAddress()
    {
        var exception = new HttpRequestException(
            "x",
            new OutboundAddressRefusedException("The host '10.0.0.5' is refused.")
        );

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("blocked_address", result);
    }

    [Fact]
    public void Classify_WhenTheChainHoldsAnAuthenticationException_ReturnsTls()
    {
        var exception = new HttpRequestException("x", new AuthenticationException("y"));

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("tls", result);
    }

    [Fact]
    public void Classify_WhenTheChainHoldsAHostNotFoundSocketException_ReturnsNameResolution()
    {
        var exception = new HttpRequestException(
            "x",
            new SocketException((int)SocketError.HostNotFound)
        );

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("name_resolution", result);
    }

    [Fact]
    public void Classify_WhenTheErrorIsNameResolution_ReturnsNameResolution()
    {
        var exception = new HttpRequestException(HttpRequestError.NameResolutionError);

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("name_resolution", result);
    }

    [Fact]
    public void Classify_WhenTheChainHoldsATimeoutException_ReturnsTimeout()
    {
        var exception = new TaskCanceledException("x", new TimeoutException());

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("timeout", result);
    }

    [Fact]
    public void Classify_WhenCanceledByTheFetchTimeout_ReturnsTimeout()
    {
        var exception = new OperationCanceledException();

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: true);

        Assert.Equal("timeout", result);
    }

    [Fact]
    public void Classify_WhenCanceledByTheCaller_ReturnsCanceled()
    {
        var exception = new OperationCanceledException();

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("canceled", result);
    }

    [Fact]
    public void Classify_WhenTheResponseStatusIsNotSuccess_ReturnsHttpStatus()
    {
        var exception = new HttpRequestException("x", null, HttpStatusCode.ServiceUnavailable);

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("http_status", result);
    }

    [Fact]
    public void Classify_WhenTheConnectionFails_ReturnsConnection()
    {
        var exception = new HttpRequestException("x");

        var result = OutboundFetchFailureReasons.Classify(exception, timedOut: false);

        Assert.Equal("connection", result);
    }

    #endregion
}

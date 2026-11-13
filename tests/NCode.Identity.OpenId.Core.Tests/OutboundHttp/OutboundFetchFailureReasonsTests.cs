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

    #region IsFetchFailure Tests

    public static TheoryData<Exception> FetchFailureExceptions =>
        new()
        {
            new HttpRequestException("x"),
            new IOException("x"),
            new InvalidDataException("x"),
            new InvalidOperationException("x"),
            new FormatException("x"),
            new OperationCanceledException(),
        };

    [Theory]
    [MemberData(nameof(FetchFailureExceptions))]
    public void IsFetchFailure_WhenTheExceptionIsAFetchFailure_ReturnsTrue(Exception exception)
    {
        var result = OutboundFetchFailureReasons.IsFetchFailure(exception, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public void IsFetchFailure_WhenTheCallerCanceled_ReturnsFalse()
    {
        using var callerSource = new CancellationTokenSource();
        callerSource.Cancel();

        var result = OutboundFetchFailureReasons.IsFetchFailure(
            new OperationCanceledException(),
            callerSource.Token
        );

        Assert.False(result);
    }

    [Fact]
    public void IsFetchFailure_WhenTheExceptionIsADefect_ReturnsFalse()
    {
        var result = OutboundFetchFailureReasons.IsFetchFailure(
            new ArgumentException("x"),
            CancellationToken.None
        );

        Assert.False(result);
    }

    #endregion

    #region IsTimedOut Tests

    [Fact]
    public void IsTimedOut_WhenOnlyTheTimeoutIsCanceled_ReturnsTrue()
    {
        using var timeoutSource = new CancellationTokenSource();
        timeoutSource.Cancel();

        var result = OutboundFetchFailureReasons.IsTimedOut(timeoutSource, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public void IsTimedOut_WhenBothAreCanceled_ReturnsFalse()
    {
        using var timeoutSource = new CancellationTokenSource();
        using var callerSource = new CancellationTokenSource();
        timeoutSource.Cancel();
        callerSource.Cancel();

        var result = OutboundFetchFailureReasons.IsTimedOut(timeoutSource, callerSource.Token);

        Assert.False(result);
    }

    [Fact]
    public void IsTimedOut_WhenNeitherIsCanceled_ReturnsFalse()
    {
        using var timeoutSource = new CancellationTokenSource();

        var result = OutboundFetchFailureReasons.IsTimedOut(timeoutSource, CancellationToken.None);

        Assert.False(result);
    }

    #endregion
}

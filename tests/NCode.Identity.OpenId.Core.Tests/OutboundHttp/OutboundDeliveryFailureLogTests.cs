// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class OutboundDeliveryFailureLogTests
{
    #region Report Tests

    [Fact]
    public void Report_WhenTheUriHasUserInfoAndAQuery_PassesTheRedactedUri()
    {
        string? redactedUri = null;

        OutboundDeliveryFailureLog.Report(
            "https://user:secret@rp.example:8443/hook?t=1#f",
            new HttpRequestException("x"),
            (uriForLog, _) => redactedUri = uriForLog
        );

        Assert.Equal("https://rp.example:8443/hook", redactedUri);
    }

    [Fact]
    public void Report_WhenTheChainHoldsARefusal_PassesBlockedAddress()
    {
        string? reason = null;
        var exception = new HttpRequestException(
            "x",
            new OutboundAddressRefusedException("The host '10.0.0.5' is refused.")
        );

        OutboundDeliveryFailureLog.Report(
            "https://rp.example/hook",
            exception,
            (_, loggedReason) => reason = loggedReason
        );

        Assert.Equal(OutboundFetchFailureReasons.BlockedAddress, reason);
    }

    [Fact]
    public void Report_WhenTheChainHoldsATimeoutException_PassesTimeout()
    {
        string? reason = null;
        var exception = new HttpRequestException("x", new TimeoutException());

        OutboundDeliveryFailureLog.Report(
            "https://rp.example/hook",
            exception,
            (_, loggedReason) => reason = loggedReason
        );

        Assert.Equal(OutboundFetchFailureReasons.Timeout, reason);
    }

    [Fact]
    public void Report_WhenAnOperationIsCanceledWithoutATimeout_PassesCanceled()
    {
        string? reason = null;

        OutboundDeliveryFailureLog.Report(
            "https://rp.example/hook",
            new OperationCanceledException(),
            (_, loggedReason) => reason = loggedReason
        );

        Assert.Equal(OutboundFetchFailureReasons.Canceled, reason);
    }

    [Fact]
    public void Report_Always_CallsTheDelegateOnce()
    {
        var calls = 0;

        OutboundDeliveryFailureLog.Report(
            "https://rp.example/hook",
            new HttpRequestException("x"),
            (_, _) => calls++
        );

        Assert.Equal(1, calls);
    }

    #endregion
}

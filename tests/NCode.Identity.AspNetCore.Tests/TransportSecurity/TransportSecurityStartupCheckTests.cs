// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting.Server;
using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

public sealed class TransportSecurityStartupCheckTests : BaseTests
{
    #region StartedAsync Tests

    [Fact]
    public async Task StartedAsync_WhenTheServerIsNotKestrel_Throws()
    {
        var server = CreateStrictMock<IServer>();
        var check = new TransportSecurityStartupCheck(
            server.Object,
            new TransportCertificateProbe(),
            TimeProvider.System
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.StartedAsync(CancellationToken.None)
        );

        Assert.Equal(TransportSecurityStartupCheck.NotKestrelMessage, exception.Message);
    }

    #endregion

    #region CreateFailureMessage Tests

    [Fact]
    public void CreateFailureMessage_WithFailures_ListsEachOnItsOwnLine()
    {
        var message = TransportSecurityStartupCheck.CreateFailureMessage([
            "The server serves plain HTTP at 'http://localhost:5000'.",
            "The endpoint 'https://localhost:8443/' completed no TLS handshake.",
        ]);

        Assert.Contains("2 transport security problem(s)", message);
        Assert.Contains(
            $"  - The server serves plain HTTP at 'http://localhost:5000'.{Environment.NewLine}",
            message
        );
        Assert.Contains(
            $"  - The endpoint 'https://localhost:8443/' completed no TLS handshake.{Environment.NewLine}",
            message
        );
        Assert.Contains("Kestrel:Certificates:Default", message);
    }

    #endregion
}

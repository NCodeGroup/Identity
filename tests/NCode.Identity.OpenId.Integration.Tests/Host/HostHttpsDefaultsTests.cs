// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using NCode.Identity.OpenId.Host;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Host;

/// <summary>
/// Provides unit tests for the <see cref="HostHttpsDefaults"/> class.
/// </summary>
public class HostHttpsDefaultsTests
{
    #region Configure Tests

    [Fact]
    public void Configure_Always_AcceptsOnlyTls12AndTls13()
    {
        var https = new HttpsConnectionAdapterOptions();

        HostHttpsDefaults.Configure(https);

        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, https.SslProtocols);
    }

    [Fact]
    public void Configure_Always_AcceptsAnyClientCertificateAtTheHandshake()
    {
        var https = new HttpsConnectionAdapterOptions();

        HostHttpsDefaults.Configure(https);

        Assert.NotNull(https.ClientCertificateValidation);
    }

    [Fact]
    public void Configure_Always_SetsTheAuthenticationCallback()
    {
        var https = new HttpsConnectionAdapterOptions();

        HostHttpsDefaults.Configure(https);

        Assert.NotNull(https.OnAuthenticate);
    }

    #endregion

    #region PermittedCipherSuites Tests

    [Fact]
    public void PermittedCipherSuites_Always_IsNotEmpty()
    {
        Assert.NotEmpty(HostHttpsDefaults.PermittedCipherSuites);
    }

    [Fact]
    public void PermittedCipherSuites_Always_ContainsOnlyAeadSuites()
    {
        foreach (var cipherSuite in HostHttpsDefaults.PermittedCipherSuites)
        {
            var name = cipherSuite.ToString();

            Assert.Contains("_GCM_", name);
        }
    }

    [Fact]
    public void PermittedCipherSuites_Always_ContainsOnlyTls13OrEphemeralEllipticCurveSuites()
    {
        foreach (var cipherSuite in HostHttpsDefaults.PermittedCipherSuites)
        {
            var name = cipherSuite.ToString();

            Assert.True(
                name.StartsWith("TLS_AES_", StringComparison.Ordinal)
                    || name.StartsWith("TLS_ECDHE_", StringComparison.Ordinal),
                name
            );
        }
    }

    #endregion

    #region OnAuthenticate Tests

    [Fact]
    public void OnAuthenticate_OnWindows_LeavesTheCipherSuitesPolicyNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sslOptions = InvokeOnAuthenticate();

        Assert.Null(sslOptions.CipherSuitesPolicy);
    }

    [Fact]
    public void OnAuthenticate_OnOtherPlatforms_AssignsTheCipherSuitesPolicy()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var sslOptions = InvokeOnAuthenticate();

        Assert.NotNull(sslOptions.CipherSuitesPolicy);
    }

    private static SslServerAuthenticationOptions InvokeOnAuthenticate()
    {
        var https = new HttpsConnectionAdapterOptions();
        HostHttpsDefaults.Configure(https);

        var onAuthenticate = https.OnAuthenticate;
        Assert.NotNull(onAuthenticate);

        var sslOptions = new SslServerAuthenticationOptions();
        onAuthenticate(null!, sslOptions);
        return sslOptions;
    }

    #endregion
}

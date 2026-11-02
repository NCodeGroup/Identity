// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class MutualTlsForwardingOptionsValidatorTests
{
    private MutualTlsForwardingOptionsValidator Validator { get; } =
        new([
            new Base64DerClientCertificateHeaderDecoder(),
            new PemClientCertificateHeaderDecoder(),
        ]);

    #region Validate Tests

    [Fact]
    public void Validate_WhenNoHeaderConfigured_Succeeds()
    {
        var options = new MutualTlsForwardingOptions { Format = "Unregistered" };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenFormatRegistered_Succeeds()
    {
        var options = new MutualTlsForwardingOptions
        {
            CertificateHeaderName = "X-Client-Cert",
            Format = ClientCertificateHeaderFormats.Pem,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenFormatNotRegistered_FailsNamingTheRegisteredFormats()
    {
        var options = new MutualTlsForwardingOptions
        {
            CertificateHeaderName = "X-Client-Cert",
            Format = "base64der",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("'base64der'", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Base64Der, Pem", result.FailureMessage, StringComparison.Ordinal);
    }

    #endregion
}

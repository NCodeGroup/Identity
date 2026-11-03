// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class ClientCertificateRevocationOptionsValidatorTests
{
    private ClientCertificateRevocationOptionsValidator Validator { get; } =
        new(
            new DefaultClientCertificateRevocationSourceCatalog([
                new DefaultClientCertificateRevocationSourceDataSource(Mock.Of<INullChangeToken>()),
            ])
        );

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultsAreKept_Succeeds()
    {
        var options = new ClientCertificateRevocationOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheFetchTimeoutIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { FetchTimeout = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:FetchTimeout'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheMaxResponseSizeIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { MaxResponseSizeBytes = 0 };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:MaxResponseSizeBytes'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheMaxCacheDurationIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { MaxCacheDuration = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:MaxCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheFailureCacheDurationIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions
        {
            FailureCacheDuration = TimeSpan.Zero,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:FailureCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheSourcesNameRegisteredSourcesInAnyOrder_Succeeds()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["crl", "ocsp"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheSourcesNameAnUnregisteredSource_FailsNamingItAndTheRegisteredSources()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["crl", "ocps"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "('ClientCertificateRevocation:Sources') names 'ocps', which is not a registered revocation source; the registered sources are 'ocsp', 'crl'.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheSourcesNameASourceTwice_Fails()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["crl", "crl"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "names 'crl' more than once",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}

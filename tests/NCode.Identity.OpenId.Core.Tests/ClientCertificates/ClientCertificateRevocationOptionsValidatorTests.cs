// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class ClientCertificateRevocationOptionsValidatorTests
{
    private ClientCertificateRevocationOptionsValidator Validator { get; } = new();

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

    #endregion
}

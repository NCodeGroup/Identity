// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class ClientCertificateRevocationOptionsValidatorTests
{
    // The validator is tested against descriptors of its own, so the shipped sources can change without touching it.
    private ClientCertificateRevocationOptionsValidator Validator { get; } =
        new(
            new DefaultClientCertificateRevocationSourceCatalog([
                new StaticDataSource([
                    new ClientCertificateRevocationSourceDescriptor { Name = "first", Order = 1 },
                    new ClientCertificateRevocationSourceDescriptor { Name = "second", Order = 2 },
                ]),
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
        var options = new ClientCertificateRevocationOptions { Sources = ["second", "first"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheSourcesNameAnUnregisteredSource_FailsNamingItAndTheRegisteredSources()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["first", "third"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "('ClientCertificateRevocation:Sources') names 'third', which is not a registered revocation source; the registered sources are 'first', 'second'.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheSourcesNameASourceTwice_Fails()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["first", "first"] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "names 'first' more than once",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheSourcesAreNull_Fails()
    {
        var options = new ClientCertificateRevocationOptions { Sources = null! };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "('ClientCertificateRevocation:Sources') must not be null.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheSourcesContainANullEntry_Fails()
    {
        var options = new ClientCertificateRevocationOptions { Sources = ["first", null!] };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "('ClientCertificateRevocation:Sources') must not contain a null entry.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion

    private sealed class StaticDataSource(
        IEnumerable<ClientCertificateRevocationSourceDescriptor> items
    ) : ICollectionDataSource<ClientCertificateRevocationSourceDescriptor>
    {
        public IEnumerable<ClientCertificateRevocationSourceDescriptor> Collection { get; } =
        [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}

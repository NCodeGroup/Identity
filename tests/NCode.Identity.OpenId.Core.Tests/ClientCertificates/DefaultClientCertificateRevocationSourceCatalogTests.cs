// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class DefaultClientCertificateRevocationSourceCatalogTests : BaseTests
{
    private static DefaultClientCertificateRevocationSourceCatalog CreateCatalog(
        params ClientCertificateRevocationSourceDescriptor[] descriptors
    ) => new([new StaticDataSource(descriptors)]);

    #region Constructor Tests

    [Fact]
    public void Constructor_WithTheDefaultDataSource_OrdersOcspBeforeCrl()
    {
        // The descriptors are static, so the catalog never asks the data source for its change token.
        var catalog = new DefaultClientCertificateRevocationSourceCatalog([
            new DefaultClientCertificateRevocationSourceDataSource(
                CreateStrictMock<INullChangeToken>().Object
            ),
        ]);

        Assert.Equal(
            [
                ClientCertificateRevocationSourceNames.Ocsp,
                ClientCertificateRevocationSourceNames.Crl,
            ],
            catalog.SourceNames
        );
    }

    [Fact]
    public void Constructor_OrdersByOrderThenByName()
    {
        var catalog = CreateCatalog(
            new ClientCertificateRevocationSourceDescriptor { Name = "zeta", Order = 50 },
            new ClientCertificateRevocationSourceDescriptor { Name = "crl", Order = 200 },
            new ClientCertificateRevocationSourceDescriptor { Name = "alpha", Order = 50 }
        );

        Assert.Equal(["alpha", "zeta", "crl"], catalog.SourceNames);
    }

    [Theory]
    [InlineData("")]
    [InlineData("deny list")]
    public void Constructor_WhenANameIsEmptyOrHasWhitespace_Throws(string name)
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(new ClientCertificateRevocationSourceDescriptor { Name = name })
        );
    }

    [Fact]
    public void Constructor_WhenTwoDescriptorsShareAName_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(
                new ClientCertificateRevocationSourceDescriptor { Name = "crl" },
                new ClientCertificateRevocationSourceDescriptor { Name = "crl", Order = 1 }
            )
        );

        Assert.Equal(
            "More than one client-certificate revocation source is named 'crl'.",
            exception.Message
        );
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenTheNameIsRegistered_ReturnsItsDescriptor()
    {
        var descriptor = new ClientCertificateRevocationSourceDescriptor { Name = "crl" };
        var catalog = CreateCatalog(descriptor);

        Assert.True(catalog.TryGet("crl", out var found));
        Assert.Same(descriptor, found);
    }

    [Fact]
    public void TryGet_WhenTheNameDiffersInCase_ReturnsFalse()
    {
        var catalog = CreateCatalog(
            new ClientCertificateRevocationSourceDescriptor { Name = "crl" }
        );

        Assert.False(catalog.TryGet("CRL", out _));
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

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public class DefaultClientCertificateRevocationCheckerTests : BaseTests
{
    #region Scaffold

    private static ClientCertificateRevocationContext Context(
        X509Certificate2 leaf,
        X509Certificate2 root
    ) =>
        new()
        {
            Certificate = leaf,
            Issuer = root,
            TrustAnchor = root,
        };

    private Mock<IClientCertificateRevocationSource> CreateSource(string name)
    {
        var mockSource = CreateStrictMock<IClientCertificateRevocationSource>();
        mockSource.SetupGet(x => x.Name).Returns(name).Verifiable();
        return mockSource;
    }

    private Mock<IClientCertificateRevocationSource> CreateSource(
        string name,
        ClientCertificateRevocationContext context,
        ClientCertificateRevocationStatus status
    )
    {
        var mockSource = CreateSource(name);
        mockSource
            .Setup(x => x.CheckAsync(context, CancellationToken.None))
            .ReturnsAsync(status)
            .Verifiable();
        return mockSource;
    }

    private static DefaultClientCertificateRevocationSourceCatalog CreateCatalog(
        params ClientCertificateRevocationSourceDescriptor[] descriptors
    ) => new([new StaticDataSource(descriptors)]);

    // The catalog describes each source at its position in the list, so the default order is the list's.
    private static DefaultClientCertificateRevocationChecker CreateChecker(
        IReadOnlyList<Mock<IClientCertificateRevocationSource>> sources,
        params string[] configured
    )
    {
        var catalog = CreateCatalog([
            .. sources.Select(
                (source, index) =>
                    new ClientCertificateRevocationSourceDescriptor
                    {
                        Name = source.Object.Name,
                        Order = index,
                    }
            ),
        ]);
        var options = Options.Create(
            new ClientCertificateRevocationOptions { Sources = [.. configured] }
        );
        return new DefaultClientCertificateRevocationChecker(
            sources.Select(source => source.Object),
            catalog,
            options
        );
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenASourceHasNoDescriptor_Throws()
    {
        var source = CreateSource("ocsp");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultClientCertificateRevocationChecker(
                [source.Object],
                CreateCatalog(),
                Options.Create(new ClientCertificateRevocationOptions())
            )
        );

        Assert.Contains("'ocsp'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenADescriptorHasNoSource_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultClientCertificateRevocationChecker(
                [],
                CreateCatalog(new ClientCertificateRevocationSourceDescriptor { Name = "crl" }),
                Options.Create(new ClientCertificateRevocationOptions())
            )
        );

        Assert.Contains("'crl'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenTwoSourcesShareAName_Throws()
    {
        var first = CreateSource("crl");
        var second = CreateSource("crl");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultClientCertificateRevocationChecker(
                [first.Object, second.Object],
                CreateCatalog(new ClientCertificateRevocationSourceDescriptor { Name = "crl" }),
                Options.Create(new ClientCertificateRevocationOptions())
            )
        );

        Assert.Contains("More than one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenAConfiguredSourceIsNotRegistered_Throws()
    {
        // The options validator checks the configured names against the catalog as the host starts, so only a catalog
        // that a host replaced can describe a configured name without a registered source.
        var crl = CreateSource("crl");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultClientCertificateRevocationChecker(
                [crl.Object],
                CreateCatalog(new ClientCertificateRevocationSourceDescriptor { Name = "crl" }),
                Options.Create(new ClientCertificateRevocationOptions { Sources = ["deny-list"] })
            )
        );

        Assert.Equal(
            "The configured client-certificate revocation source 'deny-list' is not registered.",
            exception.Message
        );
    }

    #endregion

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceAnswersGood_DoesNotAskTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource("first", context, ClientCertificateRevocationStatus.Good);
        var second = CreateSource("second");
        var checker = CreateChecker([first, second]);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceAnswersRevoked_DoesNotAskTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource("first", context, ClientCertificateRevocationStatus.Revoked);
        var second = CreateSource("second");
        var checker = CreateChecker([first, second]);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceIsInconclusive_AsksTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource("first", context, ClientCertificateRevocationStatus.Unknown);
        var second = CreateSource("second", context, ClientCertificateRevocationStatus.Revoked);
        var checker = CreateChecker([first, second]);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
    }

    [Fact]
    public async Task CheckAsync_WhenEverySourceIsInconclusive_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource("first", context, ClientCertificateRevocationStatus.Unknown);
        var second = CreateSource("second", context, ClientCertificateRevocationStatus.Unknown);
        var checker = CreateChecker([first, second]);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WithoutSources_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var checker = CreateChecker([]);

        var result = await checker.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheSourcesAreConfigured_AsksThemInTheConfiguredOrder()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);

        // The default order asks ocsp first; the configured order asks crl first, so ocsp is never asked.
        var ocsp = CreateSource("ocsp");
        var crl = CreateSource("crl", context, ClientCertificateRevocationStatus.Good);
        var checker = CreateChecker([ocsp, crl], "crl", "ocsp");

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
    }

    [Fact]
    public async Task CheckAsync_WhenASourceIsNotConfigured_NeverAsksIt()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var ocsp = CreateSource("ocsp");
        var crl = CreateSource("crl", context, ClientCertificateRevocationStatus.Unknown);
        var checker = CreateChecker([ocsp, crl], "crl");

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
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

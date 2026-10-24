#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Security.Cryptography.X509Certificates;
using Moq;
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

    private Mock<IClientCertificateRevocationSource> CreateSource(
        ClientCertificateRevocationContext context,
        ClientCertificateRevocationStatus status
    )
    {
        var mockSource = CreateStrictMock<IClientCertificateRevocationSource>();
        mockSource
            .Setup(x => x.CheckAsync(context, CancellationToken.None))
            .ReturnsAsync(status)
            .Verifiable();
        return mockSource;
    }

    private static DefaultClientCertificateRevocationChecker CreateChecker(
        params Mock<IClientCertificateRevocationSource>[] sources
    ) => new(sources.Select(source => source.Object));

    #endregion

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceAnswersGood_DoesNotAskTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource(context, ClientCertificateRevocationStatus.Good);
        var second = CreateStrictMock<IClientCertificateRevocationSource>();
        var checker = CreateChecker(first, second);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceAnswersRevoked_DoesNotAskTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource(context, ClientCertificateRevocationStatus.Revoked);
        var second = CreateStrictMock<IClientCertificateRevocationSource>();
        var checker = CreateChecker(first, second);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstSourceIsInconclusive_AsksTheNext()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource(context, ClientCertificateRevocationStatus.Unknown);
        var second = CreateSource(context, ClientCertificateRevocationStatus.Revoked);
        var checker = CreateChecker(first, second);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
    }

    [Fact]
    public async Task CheckAsync_WhenEverySourceIsInconclusive_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var context = Context(leaf, root);
        var first = CreateSource(context, ClientCertificateRevocationStatus.Unknown);
        var second = CreateSource(context, ClientCertificateRevocationStatus.Unknown);
        var checker = CreateChecker(first, second);

        var result = await checker.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WithoutSources_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var checker = CreateChecker();

        var result = await checker.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    #endregion
}

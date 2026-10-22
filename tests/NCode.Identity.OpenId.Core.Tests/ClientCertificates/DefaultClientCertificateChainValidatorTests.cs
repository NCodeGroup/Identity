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
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class DefaultClientCertificateChainValidatorTests : BaseTests
{
    private const string ClientId = "client-1";

    private static DefaultClientCertificateChainValidator CreateValidator(
        IClientCertificateRevocationChecker revocationChecker
    ) =>
        new(
            TimeProvider.System,
            revocationChecker,
            new Mock<ILogger<DefaultClientCertificateChainValidator>>().Object
        );

    // A strict checker without a setup: the validator must not consult it.
    private DefaultClientCertificateChainValidator CreateValidator() =>
        CreateValidator(CreateStrictMock<IClientCertificateRevocationChecker>().Object);

    private static void SetupCheck(
        Mock<IClientCertificateRevocationChecker> mockChecker,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        ClientCertificateRevocationStatus status
    ) =>
        mockChecker
            .Setup(x =>
                x.CheckAsync(
                    It.Is<X509Certificate2>(c => c.Thumbprint == certificate.Thumbprint),
                    It.Is<X509Certificate2>(c => c.Thumbprint == issuer.Thumbprint),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(status)
            .Verifiable();

    private OpenIdClient CreateClient(
        IReadOnlyCollection<string>? trustAnchors,
        string? revocationMode = OpenIdConstants.ClientCertificateRevocationMode.None
    )
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId);
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.TlsClientAuthTrustAnchors, out trustAnchors)
            )
            .Returns(trustAnchors is not null)
            .Verifiable();
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.TlsClientAuthRevocationMode, out revocationMode)
            )
            .Returns(revocationMode is not null);
        return mockClient.Object;
    }

    private static PresentedClientCertificate Present(
        X509Certificate2 certificate,
        params X509Certificate2[] intermediates
    ) => new(certificate) { Intermediates = [.. intermediates] };

    private static string ToPem(X509Certificate2 certificate) => certificate.ExportCertificatePem();

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_NoTrustAnchors_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(trustAnchors: null);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_EmptyTrustAnchors_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(trustAnchors: []);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MalformedTrustAnchor_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(["-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----"]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_LeafIssuedByTrustAnchor_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_ForwardedIntermediate_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf, intermediate), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_ConfiguredIntermediate_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var client = CreateClient([ToPem(root), ToPem(intermediate)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_OnlyIntermediateConfigured_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var client = CreateClient([ToPem(intermediate)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_IntermediateMissing_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_OtherIssuer_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(otherRoot, "CN=client");
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_SelfSignedLeaf_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateSelfSigned("CN=client");
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_ServerAuthenticationOnly_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=client",
            new ClientCertificateOptions
            {
                ExtendedKeyUsages = TestCertificates.ServerAuthenticationOnly,
            }
        );
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_Expired_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=client",
            new ClientCertificateOptions
            {
                NotBefore = DateTimeOffset.UtcNow.AddHours(-2),
                NotAfter = DateTimeOffset.UtcNow.AddHours(-1),
            }
        );
        var client = CreateClient([ToPem(root)]);

        var result = await CreateValidator()
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_RevocationModeUnset_ChecksTheLeafAgainstItsIssuer()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient([ToPem(root)], revocationMode: null);
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, ClientCertificateRevocationStatus.Good);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_CertificateRevoked_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(
            [ToPem(root)],
            OpenIdConstants.ClientCertificateRevocationMode.Online
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, ClientCertificateRevocationStatus.Revoked);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_RevocationUnknown_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(
            [ToPem(root)],
            OpenIdConstants.ClientCertificateRevocationMode.Online
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, ClientCertificateRevocationStatus.Unknown);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithAnIntermediate_ChecksTheLeafAndTheIntermediate()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var client = CreateClient(
            [ToPem(root)],
            OpenIdConstants.ClientCertificateRevocationMode.Online
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, intermediate, ClientCertificateRevocationStatus.Good);
        SetupCheck(mockChecker, intermediate, root, ClientCertificateRevocationStatus.Good);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf, intermediate), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_DistributionPointOnLoopback_IsNeverFetchedByThePlatform()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=client",
            new ClientCertificateOptions { CrlDistributionPoints = ["http://127.0.0.1:9/root.crl"] }
        );
        var client = CreateClient([ToPem(root)], revocationMode: null);
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, ClientCertificateRevocationStatus.Good);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    #endregion
}

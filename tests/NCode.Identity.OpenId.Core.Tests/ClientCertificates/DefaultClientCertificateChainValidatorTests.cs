// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        X509Certificate2 trustAnchor,
        ClientCertificateRevocationStatus status
    ) =>
        mockChecker
            .Setup(x =>
                x.CheckAsync(
                    It.Is<ClientCertificateRevocationContext>(c =>
                        c.Certificate.Thumbprint == certificate.Thumbprint
                        && c.Issuer.Thumbprint == issuer.Thumbprint
                        && c.TrustAnchor.Thumbprint == trustAnchor.Thumbprint
                        && c.Depth == 0
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(status)
            .Verifiable();

    private OpenIdClient CreateClient(
        IReadOnlyCollection<string>? trustAnchors,
        string? revocationMode = OpenIdConstants.ClientCertificateRevocationMode.None,
        bool trusted = false
    )
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        // Only a refusal logs the client id.
        if (!trusted)
        {
            mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        }

        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.TlsClientAuthTrustAnchors, out trustAnchors)
            )
            .Returns(trustAnchors is not null)
            .Verifiable();
        // A client without trust anchors is refused before its revocation mode is read.
        if (trustAnchors is { Count: > 0 })
        {
            mockSettings
                .Setup(x =>
                    x.TryGetValue(OpenIdSettingKeys.TlsClientAuthRevocationMode, out revocationMode)
                )
                .Returns(revocationMode is not null)
                .Verifiable();
        }

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
        var client = CreateClient([ToPem(root)], trusted: true);

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
        var client = CreateClient([ToPem(root)], trusted: true);

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
        var client = CreateClient([ToPem(root), ToPem(intermediate)], trusted: true);

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
        var client = CreateClient([ToPem(root)], revocationMode: null, trusted: true);
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Good);

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
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Revoked);

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
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Unknown);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_RevocationUnknownUnderSoftFail_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(
            [ToPem(root)],
            OpenIdConstants.ClientCertificateRevocationMode.OnlineSoftFail
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Unknown);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_CertificateRevokedUnderSoftFail_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=client");
        var client = CreateClient(
            [ToPem(root)],
            OpenIdConstants.ClientCertificateRevocationMode.OnlineSoftFail
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Revoked);

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
            OpenIdConstants.ClientCertificateRevocationMode.Online,
            trusted: true
        );
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, intermediate, root, ClientCertificateRevocationStatus.Good);
        SetupCheck(mockChecker, intermediate, root, root, ClientCertificateRevocationStatus.Good);

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
        var client = CreateClient([ToPem(root)], revocationMode: null, trusted: true);
        var mockChecker = CreateStrictMock<IClientCertificateRevocationChecker>();
        SetupCheck(mockChecker, leaf, root, root, ClientCertificateRevocationStatus.Good);

        var result = await CreateValidator(mockChecker.Object)
            .ValidateAsync(client, Present(leaf), CancellationToken.None);

        Assert.True(result);
    }

    #endregion
}

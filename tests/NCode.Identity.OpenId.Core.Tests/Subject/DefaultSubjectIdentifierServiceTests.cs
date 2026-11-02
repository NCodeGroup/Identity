// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultSubjectIdentifierServiceTests : BaseTests
{
    private const string PrincipalId = "principal-1";
    private const string ClientId = "client-1";
    private const string TenantId = "tenant-1";

    private Mock<ICryptoService> MockCryptoService { get; }
    private DefaultSubjectIdentifierService Service { get; }

    public DefaultSubjectIdentifierServiceTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        Service = new DefaultSubjectIdentifierService(MockCryptoService.Object);
    }

    #region Scaffolding

    // A pairwise subject is hashed: echo the composed input so the test can inspect how the derivation input is
    // assembled; the real hashing is ICryptoService's responsibility, verified in its own tests.
    private void SetupHash() =>
        MockCryptoService
            .Setup(x =>
                x.HashValue(
                    It.IsAny<string>(),
                    HashAlgorithmName.SHA256,
                    BinaryEncodings.Base64Url,
                    It.IsAny<Encoding?>()
                )
            )
            .Returns((string data, HashAlgorithmName _, string _, Encoding? _) => data)
            .Verifiable();

    private Mock<OpenIdContext> CreateContext(string tenantId = TenantId)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(tenantId).Verifiable();
        return mockContext;
    }

    private Mock<OpenIdClient> CreateClient(
        string? subjectType,
        string? sectorIdentifierUri = null,
        IReadOnlyCollection<string>? redirectUris = null,
        string? pairwiseSalt = null,
        string clientId = ClientId,
        bool fallsBackToClientId = false
    )
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SubjectType, out subjectType))
            .Returns(subjectType is not null)
            .Verifiable();
        if (subjectType != OpenIdProtocol.SubjectTypes.Pairwise)
        {
            return mockClient;
        }

        // The sector is the sector_identifier_uri's host, else the single redirect host, else the client id.
        if (sectorIdentifierUri is null)
        {
            mockClient.SetupGet(x => x.RedirectUris).Returns(redirectUris ?? []).Verifiable();
        }

        if (fallsBackToClientId)
        {
            mockClient.SetupGet(x => x.ClientId).Returns(clientId).Verifiable();
        }

        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.SectorIdentifierUri, out sectorIdentifierUri)
            )
            .Returns(sectorIdentifierUri is not null)
            .Verifiable();
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.PairwiseSalt, out pairwiseSalt))
            .Returns(pairwiseSalt is not null)
            .Verifiable();

        return mockClient;
    }

    #endregion

    [Fact]
    public void ProjectSubjectId_WhenPublic_ReturnsPrincipalIdUnchanged()
    {
        // A public subject identifier reads neither the tenant nor the hash.
        var context = CreateStrictMock<OpenIdContext>();
        var client = CreateClient(OpenIdProtocol.SubjectTypes.Public);

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public void ProjectSubjectId_WhenSubjectTypeUnset_DefaultsToPublic()
    {
        // A public subject identifier reads neither the tenant nor the hash.
        var context = CreateStrictMock<OpenIdContext>();
        var client = CreateClient(subjectType: null);

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DerivesFromSectorPrincipalTenantAndSalt()
    {
        SetupHash();
        var context = CreateContext();
        var client = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            sectorIdentifierUri: "https://sector.example.com/redirects.json",
            pairwiseSalt: "the-salt"
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.NotEqual(PrincipalId, result);
        Assert.Contains("sector.example.com", result, StringComparison.Ordinal);
        Assert.Contains(PrincipalId, result, StringComparison.Ordinal);
        Assert.Contains(TenantId, result, StringComparison.Ordinal);
        Assert.Contains("the-salt", result, StringComparison.Ordinal);
        // only the host of the sector_identifier_uri is used, never its path
        Assert.DoesNotContain("redirects.json", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwiseWithoutSectorUri_UsesSingleRedirectHost()
    {
        SetupHash();
        var context = CreateContext();
        var client = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            redirectUris: ["https://app.example.com/callback"]
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains("app.example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwiseWithMultipleRedirectHosts_FallsBackToClientId()
    {
        SetupHash();
        var context = CreateContext();
        var client = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            redirectUris: ["https://a.example.com/cb", "https://b.example.com/cb"],
            fallsBackToClientId: true
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains(ClientId, result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwiseWithoutRedirects_FallsBackToClientId()
    {
        SetupHash();
        var context = CreateContext();
        var client = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            redirectUris: [],
            fallsBackToClientId: true
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains(ClientId, result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_IsDeterministic()
    {
        SetupHash();
        var sector = "https://app.example.com/cb";

        var first = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(OpenIdProtocol.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );
        var second = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(OpenIdProtocol.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersBySector()
    {
        SetupHash();
        var clientA = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            redirectUris: ["https://a.example.com/cb"]
        );
        var clientB = CreateClient(
            OpenIdProtocol.SubjectTypes.Pairwise,
            redirectUris: ["https://b.example.com/cb"]
        );

        var subA = Service.ProjectSubjectId(CreateContext().Object, clientA.Object, PrincipalId);
        var subB = Service.ProjectSubjectId(CreateContext().Object, clientB.Object, PrincipalId);

        Assert.NotEqual(subA, subB);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersByTenant()
    {
        SetupHash();
        var sector = "https://app.example.com/cb";

        var subTenant1 = Service.ProjectSubjectId(
            CreateContext("tenant-1").Object,
            CreateClient(OpenIdProtocol.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );
        var subTenant2 = Service.ProjectSubjectId(
            CreateContext("tenant-2").Object,
            CreateClient(OpenIdProtocol.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );

        Assert.NotEqual(subTenant1, subTenant2);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersBySalt()
    {
        SetupHash();
        var sector = "https://app.example.com/cb";

        var withSaltA = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(
                OpenIdProtocol.SubjectTypes.Pairwise,
                redirectUris: [sector],
                pairwiseSalt: "salt-a"
            ).Object,
            PrincipalId
        );
        var withSaltB = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(
                OpenIdProtocol.SubjectTypes.Pairwise,
                redirectUris: [sector],
                pairwiseSalt: "salt-b"
            ).Object,
            PrincipalId
        );

        Assert.NotEqual(withSaltA, withSaltB);
    }
}

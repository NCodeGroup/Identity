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
        // echo the composed input so the test can inspect how the derivation input is assembled; the real hashing is
        // ICryptoService's responsibility, verified in its own tests.
        MockCryptoService
            .Setup(x =>
                x.HashValue(
                    It.IsAny<string>(),
                    HashAlgorithmName.SHA256,
                    BinaryEncodings.Base64Url,
                    It.IsAny<Encoding?>()
                )
            )
            .Returns((string data, HashAlgorithmName _, string _, Encoding? _) => data);
        Service = new DefaultSubjectIdentifierService(MockCryptoService.Object);
    }

    #region Scaffolding

    private Mock<OpenIdContext> CreateContext(string tenantId = TenantId)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(tenantId);
        return mockContext;
    }

    private Mock<OpenIdClient> CreateClient(
        string? subjectType,
        string? sectorIdentifierUri = null,
        IReadOnlyCollection<string>? redirectUris = null,
        string? pairwiseSalt = null,
        string clientId = ClientId
    )
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);
        mockClient.SetupGet(x => x.ClientId).Returns(clientId);
        mockClient.SetupGet(x => x.RedirectUris).Returns(redirectUris ?? []);

        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SubjectType, out subjectType))
            .Returns(subjectType is not null);
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.SectorIdentifierUri, out sectorIdentifierUri)
            )
            .Returns(sectorIdentifierUri is not null);
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.PairwiseSalt, out pairwiseSalt))
            .Returns(pairwiseSalt is not null);

        return mockClient;
    }

    #endregion

    [Fact]
    public void ProjectSubjectId_WhenPublic_ReturnsPrincipalIdUnchanged()
    {
        var context = CreateContext();
        var client = CreateClient(OpenIdConstants.SubjectTypes.Public);

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public void ProjectSubjectId_WhenSubjectTypeUnset_DefaultsToPublic()
    {
        var context = CreateContext();
        var client = CreateClient(subjectType: null);

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DerivesFromSectorPrincipalTenantAndSalt()
    {
        var context = CreateContext();
        var client = CreateClient(
            OpenIdConstants.SubjectTypes.Pairwise,
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
        var context = CreateContext();
        var client = CreateClient(
            OpenIdConstants.SubjectTypes.Pairwise,
            redirectUris: ["https://app.example.com/callback"]
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains("app.example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwiseWithMultipleRedirectHosts_FallsBackToClientId()
    {
        var context = CreateContext();
        var client = CreateClient(
            OpenIdConstants.SubjectTypes.Pairwise,
            redirectUris: ["https://a.example.com/cb", "https://b.example.com/cb"]
        );

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains(ClientId, result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwiseWithoutRedirects_FallsBackToClientId()
    {
        var context = CreateContext();
        var client = CreateClient(OpenIdConstants.SubjectTypes.Pairwise, redirectUris: []);

        var result = Service.ProjectSubjectId(context.Object, client.Object, PrincipalId);

        Assert.Contains(ClientId, result, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_IsDeterministic()
    {
        var sector = "https://app.example.com/cb";

        var first = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(OpenIdConstants.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );
        var second = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(OpenIdConstants.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersBySector()
    {
        var clientA = CreateClient(
            OpenIdConstants.SubjectTypes.Pairwise,
            redirectUris: ["https://a.example.com/cb"]
        );
        var clientB = CreateClient(
            OpenIdConstants.SubjectTypes.Pairwise,
            redirectUris: ["https://b.example.com/cb"]
        );

        var subA = Service.ProjectSubjectId(CreateContext().Object, clientA.Object, PrincipalId);
        var subB = Service.ProjectSubjectId(CreateContext().Object, clientB.Object, PrincipalId);

        Assert.NotEqual(subA, subB);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersByTenant()
    {
        var sector = "https://app.example.com/cb";

        var subTenant1 = Service.ProjectSubjectId(
            CreateContext("tenant-1").Object,
            CreateClient(OpenIdConstants.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );
        var subTenant2 = Service.ProjectSubjectId(
            CreateContext("tenant-2").Object,
            CreateClient(OpenIdConstants.SubjectTypes.Pairwise, redirectUris: [sector]).Object,
            PrincipalId
        );

        Assert.NotEqual(subTenant1, subTenant2);
    }

    [Fact]
    public void ProjectSubjectId_WhenPairwise_DiffersBySalt()
    {
        var sector = "https://app.example.com/cb";

        var withSaltA = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(
                OpenIdConstants.SubjectTypes.Pairwise,
                redirectUris: [sector],
                pairwiseSalt: "salt-a"
            ).Object,
            PrincipalId
        );
        var withSaltB = Service.ProjectSubjectId(
            CreateContext().Object,
            CreateClient(
                OpenIdConstants.SubjectTypes.Pairwise,
                redirectUris: [sector],
                pairwiseSalt: "salt-b"
            ).Object,
            PrincipalId
        );

        Assert.NotEqual(withSaltA, withSaltB);
    }
}

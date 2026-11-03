// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

public class ClientSecretJwtClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    private const string Algorithm = "HS256";

    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt;

    #region Scaffolding

    private Mock<IAlgorithmCollectionProvider> MockAlgorithmProvider { get; }

    private Mock<IAlgorithmCollection> MockAlgorithms { get; }

    public ClientSecretJwtClientAuthenticationHandlerTests()
    {
        MockAlgorithmProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        MockAlgorithms = CreateStrictMock<IAlgorithmCollection>();
    }

    // The validator, the replay cache, the key resolver and the profile catalog are strict and never set up: no test
    // reaches an assertion whose signature is verified.
    private ClientSecretJwtClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<ClientSecretJwtClientAuthenticationHandler>(),
            CreateStrictMock<IJwtValidator>().Object,
            MockAlgorithmProvider.Object,
            CreateStrictMock<IClientAssertionReplayCache>().Object,
            CreateStrictMock<IClientKeyResolver>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object
        );

    // The handler owns an assertion whose algorithm is keyed only by symmetric keys.
    private void SetupSymmetricAlgorithm()
    {
        var mockAlgorithm = CreateStrictMock<SignatureAlgorithm>();
        mockAlgorithm
            .SetupGet(x => x.KeyTypes)
            .Returns([new AlgorithmKeyType(typeof(SymmetricSecretKey), [])])
            .Verifiable();
        var algorithm = mockAlgorithm.Object;
        MockAlgorithms
            .Setup(x => x.TryGetSignatureAlgorithm(Algorithm, out algorithm))
            .Returns(true)
            .Verifiable();
        MockAlgorithmProvider
            .SetupGet(x => x.Collection)
            .Returns(MockAlgorithms.Object)
            .Verifiable();
    }

    // A compact JWS whose signature no test verifies: the header and the payload are read before any key is resolved.
    private void SetupAssertionForm(string iss, string sub) =>
        SetupForm(
            new Dictionary<string, StringValues>
            {
                ["client_assertion_type"] = OpenIdProtocol.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] =
                    $"{EncodeSegment($$"""{"alg":"{{Algorithm}}"}""")}.{EncodeSegment($$"""{"iss":"{{iss}}","sub":"{{sub}}"}""")}.c2ln",
            }
        );

    private static string EncodeSegment(string json) =>
        Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(json));

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAttemptLimitIsReached_RefusesBeforeResolvingTheClient()
    {
        // The limit is asked once the assertion's algorithm says the request is this method's, so the refusal is
        // audited with client_secret_jwt; the strict client resolver proves no client is looked up. See ADR-1399.
        SetupAssertionForm(iss: ClientId, sub: ClientId);
        SetupErrorFactory();
        SetupSymmetricAlgorithm();
        SetupAttemptRefused();
        SetupAudit("attempt_limit_reached");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertAttemptLimitRefusal(result);
    }

    #endregion
}

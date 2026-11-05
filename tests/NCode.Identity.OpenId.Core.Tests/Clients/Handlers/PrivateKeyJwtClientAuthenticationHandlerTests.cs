// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using Microsoft.AspNetCore.Http;
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

public class PrivateKeyJwtClientAuthenticationHandlerTests : ClientAuthenticationHandlerTestBase
{
    // The client identifier a failure that names no client is audited with.
    private const string UnknownClientId = "unknown";

    private const string Algorithm = "PS256";

    protected override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt;

    #region Scaffolding

    private Mock<IAlgorithmCollectionProvider> MockAlgorithmProvider { get; }

    private Mock<IAlgorithmCollection> MockAlgorithms { get; }

    public PrivateKeyJwtClientAuthenticationHandlerTests()
    {
        MockAlgorithmProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        MockAlgorithms = CreateStrictMock<IAlgorithmCollection>();
    }

    // The validator, the replay cache, the key resolver and the profile catalog are strict and never set up: no test
    // reaches an assertion whose signature is verified.
    private PrivateKeyJwtClientAuthenticationHandler CreateHandler() =>
        new(
            MockClientResolver.Object,
            MockClientFactory.Object,
            MockAuditEventRecorder.Object,
            MockAttemptLimiter.Object,
            CreateLogger<PrivateKeyJwtClientAuthenticationHandler>(),
            CreateStrictMock<IJwtValidator>().Object,
            MockAlgorithmProvider.Object,
            CreateStrictMock<IClientAssertionReplayCache>().Object,
            CreateStrictMock<IClientKeyResolver>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object
        );

    // The handler owns an assertion whose algorithm is keyed only by asymmetric keys.
    private void SetupAsymmetricAlgorithm()
    {
        var mockAlgorithm = CreateStrictMock<SignatureAlgorithm>();
        mockAlgorithm
            .SetupGet(x => x.KeyTypes)
            .Returns([new AlgorithmKeyType(typeof(RsaSecretKey), [])])
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
    public async Task AuthenticateClientAsync_WhenTheAssertionNamesTwoClients_CountsTheFailureWithoutAClientId()
    {
        // An assertion whose iss and sub differ names no client, so the failure is asked about and counted for the
        // address alone and audited as unknown; the strict client resolver proves no client is looked up. See ADR-1399.
        SetupAssertionForm(iss: ClientId, sub: "client-2");
        SetupErrorFactory();
        SetupAsymmetricAlgorithm();
        SetupAttemptAdmitted(clientId: null);
        SetupFailureCounted(clientId: null);
        SetupAudit("assertion_iss_sub_mismatch", UnknownClientId);
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAssertionNamesTwoClientsAtTheLimit_RefusesWithoutCounting()
    {
        SetupAssertionForm(iss: ClientId, sub: "client-2");
        SetupErrorFactory();
        SetupAsymmetricAlgorithm();
        SetupAttemptRefused(clientId: null);
        SetupAudit("attempt_limit_reached", UnknownClientId);
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertAttemptLimitRefusal(result);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheAttemptLimitIsReached_RefusesBeforeResolvingTheClient()
    {
        SetupAssertionForm(iss: ClientId, sub: ClientId);
        SetupErrorFactory();
        SetupAsymmetricAlgorithm();
        SetupAttemptRefused();
        SetupAudit("attempt_limit_reached");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertAttemptLimitRefusal(result);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsUnknown_CountsTheFailure()
    {
        SetupAssertionForm(iss: ClientId, sub: ClientId);
        SetupErrorFactory();
        SetupTenant();
        SetupAsymmetricAlgorithm();
        SetupAttemptAdmitted();
        SetupResolvedClient(null);
        SetupFailureCounted();
        SetupAudit("client_not_found");
        var handler = CreateHandler();

        var result = await handler.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertInvalidClient(result, StatusCodes.Status400BadRequest);
    }

    #endregion
}

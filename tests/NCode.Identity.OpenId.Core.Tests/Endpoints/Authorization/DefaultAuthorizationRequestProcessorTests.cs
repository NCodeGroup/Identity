// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization;

public class DefaultAuthorizationRequestProcessorTests : BaseTests
{
    private const string State = "state-1";

    private Mock<OpenIdContext> MockContext { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<OpenIdClient> MockClient { get; }
    private Mock<IAuthorizationRequest> MockAuthorizationRequest { get; }
    private AuthorizationResponseDelivery Delivery { get; }

    public DefaultAuthorizationRequestProcessorTests()
    {
        MockContext = CreateStrictMock<OpenIdContext>();
        MockMediator = CreateStrictMock<IMediator>();
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockClient = CreateStrictMock<OpenIdClient>();
        MockAuthorizationRequest = CreateStrictMock<IAuthorizationRequest>();
        Delivery = new AuthorizationResponseDelivery(
            "https://rp.example/callback",
            new QueryAuthorizationResponseModeHandler(),
            State
        );
    }

    private DefaultAuthorizationRequestProcessor CreateProcessor(
        params IOpenIdExceptionTranslator[] translators
    ) => new(CreateStrictMock<IContinueService>().Object, translators);

    private void SetupMediatorThrows(Exception exception)
    {
        MockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateAuthorizationRequestCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(exception)
            .Verifiable();
    }

    private DefaultOpenIdError CreateError(string code) => new(MockEnvironment.Object, code);

    #region ProcessRequestAsync Tests

    [Fact]
    public async Task ProcessRequestAsync_WhenTranslatorAnswersForException_DeliversTranslatedErrorWithState()
    {
        var thrown = new OwnerRootKeyUnavailableException(SecretOwner.ForTenant("acme"));
        SetupMediatorThrows(thrown);
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();

        var translated = CreateError(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable);
        IOpenIdError? translatedError = translated;

        var mockTranslator = CreateStrictMock<IOpenIdExceptionTranslator>();
        mockTranslator
            .Setup(x => x.TryTranslate(MockEnvironment.Object, thrown, out translatedError))
            .Returns(true)
            .Verifiable();

        var processor = CreateProcessor(mockTranslator.Object);

        var disposition = await processor.ProcessRequestAsync(
            MockContext.Object,
            MockClient.Object,
            MockAuthorizationRequest.Object,
            Delivery,
            CancellationToken.None
        );

        var result = Assert.IsType<AuthorizationResult>(disposition.HttpResult);
        var error = Assert.IsAssignableFrom<IOpenIdError>(result.Error);
        Assert.Same(translated, error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, error.Code);
        Assert.Equal(State, error.State);
        Assert.Same(Delivery, result.Delivery);
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenTranslatedErrorHasStatusCode_DeliversErrorWithoutStatusCode()
    {
        var thrown = new OwnerRootKeyUnavailableException(SecretOwner.ForTenant("acme"));
        SetupMediatorThrows(thrown);
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();

        var translated = CreateError(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable);
        translated.StatusCode = 503;
        IOpenIdError? translatedError = translated;

        var mockTranslator = CreateStrictMock<IOpenIdExceptionTranslator>();
        mockTranslator
            .Setup(x => x.TryTranslate(MockEnvironment.Object, thrown, out translatedError))
            .Returns(true)
            .Verifiable();

        var processor = CreateProcessor(mockTranslator.Object);

        var disposition = await processor.ProcessRequestAsync(
            MockContext.Object,
            MockClient.Object,
            MockAuthorizationRequest.Object,
            Delivery,
            CancellationToken.None
        );

        var result = Assert.IsType<AuthorizationResult>(disposition.HttpResult);
        var error = Assert.IsAssignableFrom<IOpenIdError>(result.Error);
        Assert.Same(translated, error);
        Assert.Null(error.StatusCode);
        Assert.Equal(State, error.State);
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenNoTranslatorAnswersForException_DeliversServerErrorWithState()
    {
        var thrown = new InvalidOperationException("failure");
        SetupMediatorThrows(thrown);
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();

        var created = CreateError(OpenIdProtocol.ErrorCodes.ServerError);
        var mockErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(OpenIdProtocol.ErrorCodes.ServerError))
            .Returns(created)
            .Verifiable();
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();

        var mockTranslator = CreateStrictMock<IOpenIdExceptionTranslator>();
        var unansweredError = default(IOpenIdError);
        mockTranslator
            .Setup(x => x.TryTranslate(MockEnvironment.Object, thrown, out unansweredError))
            .Returns(false)
            .Verifiable();

        var processor = CreateProcessor(mockTranslator.Object);

        var disposition = await processor.ProcessRequestAsync(
            MockContext.Object,
            MockClient.Object,
            MockAuthorizationRequest.Object,
            Delivery,
            CancellationToken.None
        );

        var result = Assert.IsType<AuthorizationResult>(disposition.HttpResult);
        var error = Assert.IsAssignableFrom<IOpenIdError>(result.Error);
        Assert.Same(created, error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.ServerError, error.Code);
        Assert.Equal(State, error.State);
        Assert.Same(thrown, error.Exception);
    }

    #endregion
}

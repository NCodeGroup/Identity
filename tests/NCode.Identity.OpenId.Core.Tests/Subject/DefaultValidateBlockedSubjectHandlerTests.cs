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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultValidateBlockedSubjectHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string BlockedMessage = "The end-user is blocked.";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedPrincipalStore> MockFederatedPrincipalStore { get; }
    private DefaultValidateBlockedSubjectHandler Handler { get; }

    public DefaultValidateBlockedSubjectHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockFederatedPrincipalStore = CreateStrictMock<IFederatedPrincipalStore>();
        Handler = new DefaultValidateBlockedSubjectHandler(
            MockStoreManagerFactory.Object,
            CreateLooseMock<ILogger<DefaultValidateBlockedSubjectHandler>>().Object
        );
    }

    #region Scaffolding

    private void SetupPrincipal(PersistedFederatedPrincipal? principal)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(MockFederatedPrincipalStore.Object)
            .Verifiable();
        MockFederatedPrincipalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(principal)
            .Verifiable();
    }

    private static PersistedFederatedPrincipal Principal(bool isBlocked) =>
        new()
        {
            TenantId = TenantId,
            PrincipalId = PrincipalId,
            IsBlocked = isBlocked,
            ConcurrencyToken = string.Empty,
        };

    // The error factory is read only when the handler refuses the subject.
    private void SetupAccessDenied(Mock<OpenIdContext> mockContext, string description)
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupSet(x => x.Description = description).Verifiable();

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(OpenIdProtocol.ErrorCodes.AccessDenied))
            .Returns(mockError.Object)
            .Verifiable();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    private ValidateSubjectStandingCommand CreateCommand(
        Mock<OpenIdContext> mockContext,
        OperationDisposition<IOpenIdError> disposition
    )
    {
        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            PrincipalId
        );

        return new ValidateSubjectStandingCommand(
            mockContext.Object,
            subjectAuthentication,
            disposition
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAlreadyHasError_ShortCircuits()
    {
        var mockInitialError = CreateLooseMock<IOpenIdError>();
        var disposition = new OperationDisposition<IOpenIdError>
        {
            Error = mockInitialError.Object,
        };
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockInitialError.Object, disposition.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenThePrincipalIsBlocked_SetsAccessDenied()
    {
        SetupPrincipal(Principal(isBlocked: true));
        var mockContext = CreateStrictMock<OpenIdContext>();
        SetupAccessDenied(mockContext, BlockedMessage);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenThePrincipalIsNotBlocked_LeavesNoError()
    {
        SetupPrincipal(Principal(isBlocked: false));
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenThereIsNoPrincipal_LeavesNoError()
    {
        SetupPrincipal(principal: null);
        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    #endregion
}

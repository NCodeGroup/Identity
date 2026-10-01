#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultValidateSubjectAuthenticationHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private DefaultValidateSubjectAuthenticationHandler Handler { get; }

    public DefaultValidateSubjectAuthenticationHandlerTests()
    {
        var mockLogger = CreateLooseMock<ILogger<DefaultValidateSubjectAuthenticationHandler>>();
        Handler = new DefaultValidateSubjectAuthenticationHandler(
            TimeProvider.System,
            mockLogger.Object
        );
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication(
        string? tenantId,
        ClaimsPrincipal subject
    )
    {
        var properties = new AuthenticationProperties(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [OpenIdConstants.AuthenticationPropertyItems.TenantId] = tenantId,
            }
        );
        return new SubjectAuthentication("scheme", properties, subject, "subject-id");
    }

    private (
        ValidateSubjectAuthenticationCommand command,
        OperationDisposition<IOpenIdError> disposition,
        Mock<OpenIdContext> context,
        Mock<OpenIdTenant> tenant
    ) CreateCommand(SubjectAuthentication subjectAuthentication, IOpenIdError? initialError)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockRequest = CreateStrictMock<IOpenIdRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // ErrorFactory and Settings are read before the has-error short-circuit on every path;
        // Tenant/TenantId are read only past it, so the consuming tests set those up themselves.
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var disposition = new OperationDisposition<IOpenIdError> { Error = initialError };
        var command = new ValidateSubjectAuthenticationCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object,
            subjectAuthentication,
            disposition
        );

        return (command, disposition, mockContext, mockTenant);
    }

    private void SetupTenant(Mock<OpenIdContext> mockContext, Mock<OpenIdTenant> mockTenant)
    {
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAlreadyHasError_ShortCircuits()
    {
        var mockInitialError = CreateLooseMock<IOpenIdError>();
        var subject = CreateSubjectAuthentication(TenantId, new ClaimsPrincipal());
        var (command, disposition, _, _) = CreateCommand(subject, mockInitialError.Object);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockInitialError.Object, disposition.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantMismatch_SetsAccessDeniedError()
    {
        var subject = CreateSubjectAuthentication("other-tenant", new ClaimsPrincipal());
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectNotAuthenticated_SetsAccessDeniedError()
    {
        var subject = CreateSubjectAuthentication(
            TenantId,
            new ClaimsPrincipal(new ClaimsIdentity())
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    #endregion
}

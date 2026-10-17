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

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Authentication.Messages.Parsers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Registration.AspNetCore;
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

    private static ClaimsPrincipal CreateAuthenticatedSubject(string? acr)
    {
        var identity = new ClaimsIdentity(authenticationType: "pwd");
        if (acr is not null)
        {
            identity.AddClaim(new Claim(JoseClaimNames.Payload.Acr, acr));
        }

        return new ClaimsPrincipal(identity);
    }

    private static IParameterCollection CreateClaimsParameters(RequestClaims requestClaims)
    {
        var parameter = new Parameter<IRequestClaims>(
            new ParameterDescriptor(OpenIdAuthenticationParameters.Claims),
            AuthenticationParameterParsers.RequestClaims,
            requestClaims
        );
        return new ParameterCollection([parameter], cloneParameters: false);
    }

    private static IParameterCollection CreateMaxAgeParameters(TimeSpan maxAge)
    {
        var parameter = new Parameter<TimeSpan?>(
            new ParameterDescriptor(OpenIdCommonParameters.MaxAge),
            OpenIdCommonParameters.MaxAge.Parser,
            maxAge
        );
        return new ParameterCollection([parameter], cloneParameters: false);
    }

    private static ClaimsPrincipal CreateSubjectWithAuthTime(string authTime)
    {
        var identity = new ClaimsIdentity(authenticationType: "pwd");
        identity.AddClaim(new Claim(JoseClaimNames.Payload.AuthTime, authTime));
        return new ClaimsPrincipal(identity);
    }

    private static RequestClaims CreateEssentialAcrRequest(
        bool? essential,
        string? value = null,
        IEnumerable<string>? values = null
    ) =>
        new()
        {
            IdToken = new RequestClaimDictionary
            {
                [JoseClaimNames.Payload.Acr] = new RequestClaim
                {
                    Essential = essential,
                    Value = value,
                    Values = values,
                },
            },
        };

    private (
        ValidateSubjectAuthenticationCommand command,
        OperationDisposition<IOpenIdError> disposition,
        Mock<OpenIdContext> context,
        Mock<OpenIdTenant> tenant
    ) CreateCommand(
        SubjectAuthentication subjectAuthentication,
        IOpenIdError? initialError,
        IParameterCollection? requestParameters = null
    )
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

        // Parameters are read only once a request reaches the max_age/acr checks past the early
        // short-circuits, so only those tests supply a collection.
        if (requestParameters is not null)
        {
            mockRequest.SetupGet(x => x.Parameters).Returns(requestParameters).Verifiable();
        }

        var disposition = new OperationDisposition<IOpenIdError> { Error = initialError };
        var command = new ValidateSubjectAuthenticationCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object,
            subjectAuthentication,
            disposition,
            EnforceAuthenticationAge: true
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

    #region Essential acr Enforcement Tests

    [Fact]
    public async Task HandleAsync_WhenEssentialAcrSatisfiedViaValues_Succeeds()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject("acr-1"));
        var parameters = CreateClaimsParameters(
            CreateEssentialAcrRequest(essential: true, values: ["acr-1", "acr-2"])
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenEssentialAcrSatisfiedViaSingleValue_Succeeds()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject("acr-1"));
        var parameters = CreateClaimsParameters(
            CreateEssentialAcrRequest(essential: true, value: "acr-1")
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenEssentialAcrNotSatisfied_SetsAccessDeniedError()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject("acr-3"));
        var parameters = CreateClaimsParameters(
            CreateEssentialAcrRequest(essential: true, values: ["acr-1", "acr-2"])
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenEssentialAcrRequestedButSubjectHasNone_SetsAccessDeniedError()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject(acr: null));
        var parameters = CreateClaimsParameters(
            CreateEssentialAcrRequest(essential: true, values: ["acr-1"])
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenAcrRequestedButNotEssential_Succeeds()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject("acr-3"));
        var parameters = CreateClaimsParameters(
            CreateEssentialAcrRequest(essential: false, values: ["acr-1", "acr-2"])
        );
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenEssentialAcrWithoutSpecificValues_Succeeds()
    {
        var subject = CreateSubjectAuthentication(TenantId, CreateAuthenticatedSubject(acr: null));
        var parameters = CreateClaimsParameters(CreateEssentialAcrRequest(essential: true));
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    #endregion

    #region auth_time NumericDate Tests

    [Fact]
    public async Task HandleAsync_WhenAuthTimeOutOfRangeAndMaxAgeRequested_SetsAccessDeniedError()
    {
        var subject = CreateSubjectAuthentication(
            TenantId,
            CreateSubjectWithAuthTime("99999999999999")
        );
        var parameters = CreateMaxAgeParameters(TimeSpan.FromHours(1));
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenFractionalAuthTimeWithinMaxAge_Succeeds()
    {
        var authSeconds = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds();
        var authTime = string.Create(CultureInfo.InvariantCulture, $"{authSeconds}.5");
        var subject = CreateSubjectAuthentication(TenantId, CreateSubjectWithAuthTime(authTime));
        var parameters = CreateMaxAgeParameters(TimeSpan.FromHours(1));
        var (command, disposition, mockContext, mockTenant) = CreateCommand(
            subject,
            initialError: null,
            parameters
        );
        SetupTenant(mockContext, mockTenant);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(disposition.HasError);
    }

    #endregion
}

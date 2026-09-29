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
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class DefaultAuthorizeDeleteTenantHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private DefaultAuthorizeDeleteTenantHandler Handler { get; }

    public DefaultAuthorizeDeleteTenantHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        Handler = new DefaultAuthorizeDeleteTenantHandler(MockAuthorizationService.Object);
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = "tenant-1",
            ConcurrencyToken = string.Empty,
            DomainName = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = "tenant-1",
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new JsonObject()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = "tenant-1",
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private static HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorized_LeavesDispositionClean()
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

        var disposition = new OperationDisposition<ManagementError>();
        var command = new ValidateDeleteTenantCommand(
            CreateHttpContext(authenticated: true),
            CreateTenant(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.Succeeded);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAuthorizedAndAuthenticated_SetsForbidden()
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        var disposition = new OperationDisposition<ManagementError>();
        var command = new ValidateDeleteTenantCommand(
            CreateHttpContext(authenticated: true),
            CreateTenant(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.Equal(StatusCodes.Status403Forbidden, disposition.Error.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAuthorizedAndAnonymous_SetsUnauthorized()
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        var disposition = new OperationDisposition<ManagementError>();
        var command = new ValidateDeleteTenantCommand(
            CreateHttpContext(authenticated: false),
            CreateTenant(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.Equal(StatusCodes.Status401Unauthorized, disposition.Error.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyHasError_ShortCircuits()
    {
        var disposition = new OperationDisposition<ManagementError>
        {
            Error = new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "x",
            },
        };
        var command = new ValidateDeleteTenantCommand(
            CreateHttpContext(authenticated: true),
            CreateTenant(),
            disposition
        );

        // No authorization setup: a strict mock fails if AuthorizeAsync is called.
        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, disposition.Error.StatusCode);
    }
}

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

using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Registration;

public class DefaultInitialAccessTokenValidatorTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string Token = "initial-access-token";

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
            GrantKey = Token,
        };

    private void SetupGrantId(Mock<IPersistedGrantService> mockGrantService) =>
        mockGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
                    Token
                )
            )
            .Returns(CreateGrantId())
            .Verifiable();

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_EmptyToken_ReturnsNull()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.ValidateAsync(
            mockContext.Object,
            string.Empty,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ActiveGrant_ReturnsPayload()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        var grant = new PersistedGrant<InitialAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = null,
            SessionId = null,
            ExpiresWhen = DateTimeOffset.UtcNow.AddDays(1),
            Payload = new InitialAccessTokenGrant { ResourceServerId = "api" },
        };

        mockGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(grant))
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.ValidateAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.Equal("api", result?.ResourceServerId);
    }

    [Fact]
    public async Task ValidateAsync_NoGrant_ReturnsNull()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        mockGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(
                    (PersistedGrant<InitialAccessTokenGrant>?)null
                )
            )
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.ValidateAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ConsumedGrant_ReturnsNull()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        var grant = new PersistedGrant<InitialAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = null,
            SessionId = null,
            ExpiresWhen = DateTimeOffset.UtcNow.AddDays(1),
            ConsumedWhen = DateTimeOffset.UtcNow,
            Payload = new InitialAccessTokenGrant { ResourceServerId = "api" },
        };

        mockGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(grant))
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.ValidateAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_GrantWithoutExpiry_ReturnsNull()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        var grant = new PersistedGrant<InitialAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = null,
            SessionId = null,
            Payload = new InitialAccessTokenGrant { ResourceServerId = "api" },
        };

        mockGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(grant))
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.ValidateAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region TryConsumeAsync Tests

    [Fact]
    public async Task TryConsumeAsync_EmptyToken_ReturnsFalse()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.TryConsumeAsync(
            mockContext.Object,
            string.Empty,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task TryConsumeAsync_UnspentGrant_ReturnsTrue()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        var grant = new PersistedGrant<InitialAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = null,
            SessionId = null,
            ConsumedWhen = DateTimeOffset.UtcNow,
            Payload = new InitialAccessTokenGrant { ResourceServerId = "api" },
        };

        mockGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(grant))
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.TryConsumeAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task TryConsumeAsync_SpentGrant_ReturnsFalse()
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupGrantId(mockGrantService);

        mockGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<InitialAccessTokenGrant>(
                    mockContext.Object,
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<InitialAccessTokenGrant>?>(
                    (PersistedGrant<InitialAccessTokenGrant>?)null
                )
            )
            .Verifiable();

        var validator = new DefaultInitialAccessTokenValidator(mockGrantService.Object);

        var result = await validator.TryConsumeAsync(
            mockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.False(result);
    }

    #endregion
}

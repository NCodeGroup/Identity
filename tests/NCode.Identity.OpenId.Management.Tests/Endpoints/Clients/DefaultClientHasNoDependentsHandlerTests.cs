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
using Microsoft.AspNetCore.Http;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientHasNoDependentsHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private DefaultClientHasNoDependentsHandler Handler { get; }

    public DefaultClientHasNoDependentsHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        Handler = new DefaultClientHasNoDependentsHandler(MockStoreManagerFactory.Object);
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private static PersistedClient CreateClient() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new JsonObject()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private static HttpContext CreateHttpContext() =>
        new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
        };

    [Fact]
    public async Task HandleAsync_WhenNoDependents_LeavesDispositionClean()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.HasDependentsAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var disposition = new OperationDisposition<ManagementError>();
        var command = new ValidateDeleteClientCommand(
            CreateHttpContext(),
            CreateClient(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.Succeeded);
    }

    [Fact]
    public async Task HandleAsync_WhenHasDependents_SetsConflict()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.HasDependentsAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var disposition = new OperationDisposition<ManagementError>();
        var command = new ValidateDeleteClientCommand(
            CreateHttpContext(),
            CreateClient(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.Equal(StatusCodes.Status409Conflict, disposition.Error.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyHasError_ShortCircuits()
    {
        // No store setup: a strict mock fails if the store is used.
        var disposition = new OperationDisposition<ManagementError>
        {
            Error = new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "x",
            },
        };
        var command = new ValidateDeleteClientCommand(
            CreateHttpContext(),
            CreateClient(),
            disposition
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, disposition.Error.StatusCode);
    }
}

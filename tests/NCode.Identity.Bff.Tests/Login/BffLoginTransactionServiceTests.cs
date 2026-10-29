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

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffLoginTransactionServiceTests : IAsyncLifetime
{
    private ServiceProvider Provider { get; }
    private BffLoginTransactionService Service { get; }

    public BffLoginTransactionServiceTests()
    {
        var services = new ServiceCollection();
        services
            .AddSharedStateServices()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        Provider = services.BuildServiceProvider();

        var values = Provider.GetRequiredService<IExpiringValueSet>();
        var indexes = Provider.GetRequiredService<IExpiringIndexSet>();
        Service = new BffLoginTransactionService(new DefaultBffSessionStore(values, indexes));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Provider.DisposeAsync();

    private static BffLoginTransaction CreateTransaction() =>
        new()
        {
            Issuer = "https://op.example",
            RedirectUri = "https://app.example/bff/callback",
            CodeVerifier = "verifier",
            Nonce = "nonce",
            ReturnUrl = "/home",
            DPoPJwkThumbprint = "jkt",
            MaxAge = 30,
        };

    private static DateTimeOffset ExpiresWhen => DateTimeOffset.UtcNow.AddMinutes(10);

    #region StoreAsync and TakeAsync Tests

    [Fact]
    public async Task TakeAsync_AfterStore_RoundTripsTheTransaction()
    {
        var state = BffKeyDerivation.CreateSecret();
        var initiator = BffKeyDerivation.CreateSecret();
        var transaction = CreateTransaction();
        await Service.StoreAsync(
            state,
            initiator,
            transaction,
            ExpiresWhen,
            CancellationToken.None
        );

        var result = await Service.TakeAsync(state, initiator, CancellationToken.None);

        Assert.Null(result.FailureReason);
        Assert.Equal(transaction, result.Transaction);
    }

    [Fact]
    public async Task TakeAsync_SecondTake_IsUnknownState()
    {
        var state = BffKeyDerivation.CreateSecret();
        var initiator = BffKeyDerivation.CreateSecret();
        await Service.StoreAsync(
            state,
            initiator,
            CreateTransaction(),
            ExpiresWhen,
            CancellationToken.None
        );
        await Service.TakeAsync(state, initiator, CancellationToken.None);

        var result = await Service.TakeAsync(state, initiator, CancellationToken.None);

        Assert.Null(result.Transaction);
        Assert.Equal(BffLoginFailureReasons.UnknownState, result.FailureReason);
    }

    [Fact]
    public async Task TakeAsync_AnotherInitiator_IsInitiatorMismatch()
    {
        var state = BffKeyDerivation.CreateSecret();
        await Service.StoreAsync(
            state,
            BffKeyDerivation.CreateSecret(),
            CreateTransaction(),
            ExpiresWhen,
            CancellationToken.None
        );

        var result = await Service.TakeAsync(
            state,
            BffKeyDerivation.CreateSecret(),
            CancellationToken.None
        );

        Assert.Null(result.Transaction);
        Assert.Equal(BffLoginFailureReasons.InitiatorMismatch, result.FailureReason);
    }

    [Fact]
    public async Task TakeAsync_MissingInitiator_IsInitiatorMismatch()
    {
        var state = BffKeyDerivation.CreateSecret();
        await Service.StoreAsync(
            state,
            BffKeyDerivation.CreateSecret(),
            CreateTransaction(),
            ExpiresWhen,
            CancellationToken.None
        );

        var result = await Service.TakeAsync(state, null, CancellationToken.None);

        Assert.Equal(BffLoginFailureReasons.InitiatorMismatch, result.FailureReason);
    }

    [Fact]
    public async Task TakeAsync_BadState_IsUnknownState()
    {
        var result = await Service.TakeAsync(
            "bad",
            BffKeyDerivation.CreateSecret(),
            CancellationToken.None
        );

        Assert.Equal(BffLoginFailureReasons.UnknownState, result.FailureReason);
    }

    [Fact]
    public async Task StoreAsync_ExistingState_Throws()
    {
        var state = BffKeyDerivation.CreateSecret();
        var initiator = BffKeyDerivation.CreateSecret();
        await Service.StoreAsync(
            state,
            initiator,
            CreateTransaction(),
            ExpiresWhen,
            CancellationToken.None
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Service.StoreAsync(
                state,
                initiator,
                CreateTransaction(),
                ExpiresWhen,
                CancellationToken.None
            )
        );
    }

    #endregion
}

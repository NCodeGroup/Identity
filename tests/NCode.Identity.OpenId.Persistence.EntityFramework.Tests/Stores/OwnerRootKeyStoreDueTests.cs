// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses the in-memory provider because SQLite cannot compare DateTimeOffset values in a query.
public sealed class OwnerRootKeyStoreDueTests : IDisposable
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provider;

    public OwnerRootKeyStoreDueTests()
    {
        var serviceCollection = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        _provider = serviceCollection.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    private OwnerRootKeyStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private static PersistedOwnerRootKey CreateRootKey(
        string ownerId,
        string state = OwnerRootKeyStates.Active,
        DateTimeOffset? validatedWhen = null,
        DateTimeOffset? failedWhen = null
    ) =>
        new()
        {
            OwnerType = SecretOwnerTypes.Tenant,
            OwnerId = ownerId,
            KeyWrapperName = "aws-kms",
            KeyReference = $"arn:aws:kms:us-west-2:111122223333:key/{ownerId}",
            State = state,
            CreatedWhen = Now.AddDays(-1),
            StateChangedWhen = Now.AddHours(-3),
            ValidatedWhen = validatedWhen,
            FailedWhen = failedWhen,
            Failure = failedWhen is null ? null : "AccessDenied: revoked",
        };

    private async Task AddAsync(params PersistedOwnerRootKey[] rootKeys)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);
        foreach (var rootKey in rootKeys)
            await store.AddAsync(rootKey, CancellationToken.None);

        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> GetDueOwnersAsync(int limit = 100)
    {
        var owners = new List<string>();
        string? cursor = null;
        do
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var page = await CreateStore(context)
                .GetDuePageAsync(
                    Now.AddHours(-1),
                    Now.AddMinutes(-1),
                    new PageRequest { Limit = limit, Cursor = cursor },
                    CancellationToken.None
                );
            owners.AddRange(page.Items.Select(item => item.OwnerId));
            cursor = page.NextCursor;
        } while (cursor is not null);

        return owners;
    }

    [Fact]
    public async Task GetDuePage_SelectsActiveKeysValidatedBeforeTheCutoffAndKeysToRetry()
    {
        await AddAsync(
            CreateRootKey("never-validated"),
            CreateRootKey("validated-recently", validatedWhen: Now.AddMinutes(-10)),
            CreateRootKey("validated-long-ago", validatedWhen: Now.AddHours(-2)),
            CreateRootKey("unavailable-never-failed", OwnerRootKeyStates.Unavailable),
            CreateRootKey(
                "unavailable-just-failed",
                OwnerRootKeyStates.Unavailable,
                failedWhen: Now.AddSeconds(-10)
            ),
            CreateRootKey(
                "unavailable-failed-earlier",
                OwnerRootKeyStates.Unavailable,
                failedWhen: Now.AddMinutes(-5)
            ),
            CreateRootKey("removing-never-failed", OwnerRootKeyStates.Removing),
            CreateRootKey(
                "removing-just-failed",
                OwnerRootKeyStates.Removing,
                failedWhen: Now.AddSeconds(-10)
            ),
            CreateRootKey("unknown-state", "unknown")
        );

        var due = await GetDueOwnersAsync();

        Assert.Equal(
            [
                "never-validated",
                "validated-long-ago",
                "unavailable-never-failed",
                "unavailable-failed-earlier",
                "removing-never-failed",
                "unknown-state",
            ],
            due
        );
    }

    [Fact]
    public async Task GetDuePage_PagesByRowIdentifier()
    {
        await AddAsync(
            CreateRootKey("a"),
            CreateRootKey("b"),
            CreateRootKey("c"),
            CreateRootKey("d"),
            CreateRootKey("e")
        );

        var due = await GetDueOwnersAsync(limit: 2);

        Assert.Equal(["a", "b", "c", "d", "e"], due.Order());
        Assert.Equal(5, due.Count);
    }
}

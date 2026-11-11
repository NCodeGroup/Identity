// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses the in-memory provider because SQLite cannot compare DateTimeOffset values in a query.
public sealed class OwnerRootKeyStoreDueTests : IDisposable
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private ServiceProvider Provider { get; }

    public OwnerRootKeyStoreDueTests()
    {
        var serviceCollection = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        Provider = serviceCollection.BuildServiceProvider();
    }

    public void Dispose() => Provider.Dispose();

    private OwnerRootKeyStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), Provider.GetRequiredService<IIdGenerator<long>>(), context);

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

    private async ValueTask AddAsync(
        PersistedOwnerRootKey[] rootKeys,
        CancellationToken cancellationToken
    )
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = CreateStore(context);
        foreach (var rootKey in rootKeys)
            await store.AddAsync(rootKey, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<IReadOnlyList<string>> GetDueOwnersAsync(
        int limit,
        CancellationToken cancellationToken
    )
    {
        var owners = new List<string>();
        string? cursor = null;
        do
        {
            using var scope = Provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            var page = await CreateStore(context)
                .GetDuePageAsync(
                    Now.AddHours(-1),
                    Now.AddMinutes(-1),
                    new PageRequest { Limit = limit, Cursor = cursor },
                    cancellationToken
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
            [
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
                CreateRootKey("unknown-state", "unknown"),
            ],
            CancellationToken.None
        );

        var due = await GetDueOwnersAsync(limit: 100, CancellationToken.None);

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
            [
                CreateRootKey("a"),
                CreateRootKey("b"),
                CreateRootKey("c"),
                CreateRootKey("d"),
                CreateRootKey("e"),
            ],
            CancellationToken.None
        );

        var due = await GetDueOwnersAsync(limit: 2, CancellationToken.None);

        Assert.Equal(["a", "b", "c", "d", "e"], due.Order());
        Assert.Equal(5, due.Count);
    }
}

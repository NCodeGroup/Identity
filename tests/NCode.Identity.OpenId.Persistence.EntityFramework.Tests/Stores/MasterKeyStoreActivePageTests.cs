// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// Uses the in-memory provider because SQLite cannot compare DateTimeOffset values in a query.
public sealed class MasterKeyStoreActivePageTests : IDisposable
{
    private static DateTimeOffset CreatedWhen { get; } = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provider;

    public MasterKeyStoreActivePageTests()
    {
        var serviceCollection = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        _provider = serviceCollection.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    private static PersistedMasterKey CreateMasterKey(
        string ownerId,
        int version,
        DateTimeOffset createdWhen,
        bool retired
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = SecretOwnerTypes.Tenant,
            OwnerId = ownerId,
            Version = version,
            CreatedWhen = createdWhen,
            RetiredWhen = retired ? createdWhen.AddDays(30) : null,
            RootKeyProvider = KeyWrapperNames.Local,
            RootKeyId = "root-key",
            RootWrappedKey = "root-wrapped-key",
            RecoveryKeyId = null,
            RecoveryWrappedKey = null,
            KeyCheck = "key-check",
        };

    #region GetActivePageAsync Tests

    [Fact]
    public async Task GetActivePage_ReturnsOnlyActiveRowsCreatedBeforeTheInstant()
    {
        var retired = CreateMasterKey("tenant-one", 1, CreatedWhen, retired: true);
        var old = CreateMasterKey("tenant-one", 2, CreatedWhen, retired: false);
        var recent = CreateMasterKey("tenant-two", 1, CreatedWhen.AddDays(10), retired: false);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var store = new MasterKeyStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            context
        );
        foreach (var key in new[] { retired, old, recent })
            await store.AddAsync(key, CancellationToken.None);
        await context.SaveChangesAsync();

        var result = await store.GetActivePageAsync(
            CreatedWhen.AddDays(5),
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(old.KeyId, Assert.Single(result.Items).KeyId);
    }

    #endregion
}

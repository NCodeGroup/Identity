#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Text.Json;
using IdGen;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="IPersistedGrantService"/> abstraction.
/// </summary>
public class DefaultPersistedGrantService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IIdGenerator<long> idGenerator,
    IStoreManagerFactory storeManagerFactory,
    OpenIdServer openIdServer
) : IPersistedGrantService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private OpenIdServer OpenIdServer { get; } = openIdServer;

    private string GetHashedKey(string grantKey) =>
        CryptoService.HashValue(
            grantKey,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64); // TODO: will this cause collation issues?

    /// <inheritdoc />
    public async ValueTask<TimePeriod> AddAsync<TPayload>(
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var id = IdGenerator.CreateId();
        var hashedKey = GetHashedKey(grantId.GrantKey);

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var timePeriod = new TimePeriod(createdWhen, lifetime);

        var payload = JsonSerializer.SerializeToElement(
            grant.Payload,
            OpenIdServer.JsonSerializerOptions);

        var envelope = new PersistedGrant
        {
            Id = id,
            TenantId = grantId.TenantId,
            GrantType = grantId.GrantType,
            HashedKey = hashedKey,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            CreatedWhen = timePeriod.StartTime,
            ExpiresWhen = timePeriod.EndTime,
            ConsumedWhen = null,
            Payload = payload
        };

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.AddAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        return timePeriod;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> TryGetAsync<TPayload>(
        PersistedGrantId grantId,
        bool singleUse,
        bool setConsumed,
        CancellationToken cancellationToken)
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.TryGetAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            cancellationToken);

        if (envelope == null)
            return default;

        if (envelope.ExpiresWhen <= utcNow)
            return default;

        var isConsumed = envelope.ConsumedWhen.HasValue;

        if (singleUse && isConsumed)
            return default;

        var isDirty = false;
        if (setConsumed && !isConsumed)
        {
            await store.SetConsumedOnceAsync(
                grantId.TenantId,
                grantId.GrantType,
                hashedKey,
                utcNow,
                cancellationToken);

            isDirty = true;
        }

        var payload = envelope.Payload.Deserialize<TPayload>(
            OpenIdServer.JsonSerializerOptions);

        if (isDirty)
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        if (payload is null)
            return default;

        var persistedGrant = new PersistedGrant<TPayload>
        {
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedAsync(
        PersistedGrantId grantId,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken)
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.SetConsumedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            consumedWhen.WithPrecisionInSeconds(),
            cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}

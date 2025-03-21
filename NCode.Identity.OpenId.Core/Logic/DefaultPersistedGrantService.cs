﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="IPersistedGrantService"/> abstraction.
/// </summary>
public class DefaultPersistedGrantService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IIdGenerator<long> idGenerator,
    IStoreManagerFactory storeManagerFactory
) : IPersistedGrantService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private string GetHashedKey(string grantKey) =>
        CryptoService.HashValue(
            grantKey,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64
        );

    /// <inheritdoc />
    public async ValueTask AddAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    )
    {
        if (grant.Status != PersistedGrantStatus.Active)
            throw new InvalidOperationException("The grant must be active.");

        var openIdEnvironment = openIdContext.Environment;

        var id = IdGenerator.CreateId();
        var hashedKey = GetHashedKey(grantId.GrantKey);

        var expiresWhen = createdWhen + lifetime;

        var payloadJson = JsonSerializer.SerializeToElement(
            grant.Payload,
            openIdEnvironment.JsonSerializerOptions
        );

        var envelope = new PersistedGrant
        {
            Id = id,
            TenantId = grantId.TenantId,
            GrantType = grantId.GrantType,
            HashedKey = hashedKey,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = payloadJson
        };

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.AddAsync(envelope, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    private static PersistedGrantStatus GetStatus(DateTimeOffset utcNow, PersistedGrant envelope)
    {
        if (envelope.RevokedWhen is not null)
            return PersistedGrantStatus.Revoked;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (envelope.ExpiresWhen <= utcNow)
            return PersistedGrantStatus.Expired;

        return PersistedGrantStatus.Active;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> TryGetAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var hashedKey = GetHashedKey(grantId.GrantKey);
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.TryGetAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            cancellationToken
        );

        if (envelope == null)
            return null;

        var payload = envelope.PayloadJson.Deserialize<TPayload>(
            openIdEnvironment.JsonSerializerOptions
        );

        if (payload is null)
            throw new InvalidOperationException("The payload could not be deserialized.");

        var status = GetStatus(utcNow, envelope);
        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> TryConsumeOnce<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var hashedKey = GetHashedKey(grantId.GrantKey);
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.TryGetAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            cancellationToken
        );

        if (envelope == null)
            return null;

        var status = GetStatus(utcNow, envelope);
        var isConsumed = envelope.ConsumedWhen is not null;
        if (status != PersistedGrantStatus.Active || isConsumed)
            return null;

        await store.SetConsumedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            utcNow,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        var payload = envelope.PayloadJson.Deserialize<TPayload>(
            openIdEnvironment.JsonSerializerOptions
        );

        if (payload is null)
            throw new InvalidOperationException("The payload could not be deserialized.");

        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken
    )
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.SetConsumedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            consumedWhen,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask SetRevokedAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.SetRevokedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            revokedWhen,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateExpirationAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.UpdateExpirationAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            expiresWhen,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}

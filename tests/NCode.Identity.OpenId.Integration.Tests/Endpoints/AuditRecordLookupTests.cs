// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The management API looks up a tenant's audit records in the audit store, newest first, by the envelope fields an
// operator investigates with. See ADR-1303.
public class AuditRecordLookupTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    // A name no audited operation of the host uses, so the records the host writes as the test runs never match.
    private const string SeededEventName = "it.audit_lookup.seeded";

    private static BootstrapAdminApplicationFactory CreateFactory() => new();

    private static PersistedAuditRecord Record(string tenantId, string clientId) =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = DateTimeOffset.UtcNow,
            EventName = SeededEventName,
            EventCategory = "management",
            Outcome = "success",
            Severity = "info",
            TenantId = tenantId,
            ClientId = clientId,
            ActorType = "administrator",
            ActorId = "admin-1",
            Resources = [new PersistedAuditResource { Type = "client", Id = clientId }],
            Record = JsonElement.Parse(
                $$"""{"event_name":"{{SeededEventName}}","client_id":"{{clientId}}"}"""
            ),
        };

    private static async Task SeedAsync(
        HostApplicationFactory factory,
        params PersistedAuditRecord[] records
    )
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        await storeManager.GetStore<IAuditRecordStore>().AddAsync(records, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    #region ListAuditRecords Tests

    [Fact]
    public async Task ListAuditRecords_ReturnsTheTenantsMatchingRecordsNewestFirst()
    {
        using var factory = CreateFactory();
        var older = Record(HostApplicationFactory.TenantId, "client-a");
        var newer = Record(HostApplicationFactory.TenantId, "client-b");
        await SeedAsync(factory, older, newer, Record("another-tenant", "client-a"));
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapAdminApplicationFactory.ClientId,
            BootstrapAdminApplicationFactory.ClientSecret
        );

        using var response = await administrator.GetAsync(
            $"{TenantApiPath}/audit-records?eventName={SeededEventName}"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var items = JsonElement.Parse(body).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            [newer.EventId, older.EventId],
            items.Select(item => item.GetProperty("eventId").GetGuid())
        );
        var first = items[0];
        Assert.Equal("client-b", first.GetProperty("clientId").GetString());
        Assert.Equal("client-b", first.GetProperty("resources")[0].GetProperty("id").GetString());
        Assert.Equal(
            SeededEventName,
            first.GetProperty("record").GetProperty("event_name").GetString()
        );
    }

    [Fact]
    public async Task ListAuditRecords_WithAResourceFilter_ReturnsOnlyTheRecordsNamingIt()
    {
        using var factory = CreateFactory();
        var match = Record(HostApplicationFactory.TenantId, "client-a");
        await SeedAsync(factory, match, Record(HostApplicationFactory.TenantId, "client-b"));
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapAdminApplicationFactory.ClientId,
            BootstrapAdminApplicationFactory.ClientSecret
        );

        using var response = await administrator.GetAsync(
            $"{TenantApiPath}/audit-records?eventName={SeededEventName}&resourceType=client&resourceId=client-a"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var item = Assert.Single(JsonElement.Parse(body).GetProperty("items").EnumerateArray());
        Assert.Equal(match.EventId, item.GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task ListAuditRecords_WithoutAToken_IsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{TenantApiPath}/audit-records");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}

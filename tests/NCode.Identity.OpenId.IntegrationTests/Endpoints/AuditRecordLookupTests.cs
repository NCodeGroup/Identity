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

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// The management API looks up a tenant's audit records in the audit store, newest first, by the envelope fields an
// operator investigates with. See ADR-0303.
public class AuditRecordLookupTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    // A name no audited operation of the host uses, so the records the host writes as the test runs never match.
    private const string SeededEventName = "it.audit_lookup.seeded";

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

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
            BootstrapClientId,
            BootstrapClientSecret
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
            BootstrapClientId,
            BootstrapClientSecret
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

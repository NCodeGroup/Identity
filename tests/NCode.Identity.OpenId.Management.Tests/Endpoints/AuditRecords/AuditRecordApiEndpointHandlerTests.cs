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

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.AuditRecords;

public sealed class AuditRecordApiEndpointHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private AuditRecordApiEndpointHandler Handler { get; }

    public AuditRecordApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        Handler = new AuditRecordApiEndpointHandler(
            MockRepository.Create<IStoreManagerFactory>().Object,
            MockRepository.Create<IAuthorizationService>().Object,
            MockRepository.Create<ICryptoService>().Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region ToAuditRecordResource Tests

    [Fact]
    public void ToAuditRecordResource_CopiesTheEnvelopeAndTheRecord()
    {
        var record = new PersistedAuditRecord
        {
            EventId = Guid.CreateVersion7(),
            EventTime = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
            EventName = "client.updated",
            EventCategory = "management",
            Outcome = "success",
            Severity = "info",
            TenantId = "tenant-1",
            SubjectId = "user-1",
            ClientId = "client-1",
            ActorType = "administrator",
            ActorId = "admin-1",
            TraceId = "trace-1",
            SourceIp = "203.0.113.7",
            Resources = [new PersistedAuditResource { Type = "client", Id = "client-1" }],
            Record = JsonElement.Parse(
                """{"event_name":"client.updated","request":{"path":"/api"}}"""
            ),
        };

        var resource = Handler.ToAuditRecordResource(record);

        Assert.Equal(record.EventId, resource.EventId);
        Assert.Equal(record.EventTime, resource.EventTime);
        Assert.Equal("client.updated", resource.EventName);
        Assert.Equal("management", resource.EventCategory);
        Assert.Equal("success", resource.Outcome);
        Assert.Equal("info", resource.Severity);
        Assert.Equal("user-1", resource.SubjectId);
        Assert.Equal("client-1", resource.ClientId);
        Assert.Equal("administrator", resource.ActorType);
        Assert.Equal("admin-1", resource.ActorId);
        Assert.Equal("trace-1", resource.TraceId);
        Assert.Equal("203.0.113.7", resource.SourceIp);
        var reference = Assert.Single(resource.Resources);
        Assert.Equal(("client", "client-1"), (reference.Type, reference.Id));
        Assert.Equal(
            "/api",
            resource.Record.GetProperty("request").GetProperty("path").GetString()
        );
    }

    #endregion
}

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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.AuditRecords;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.AuditRecords;

/*

GET    api/tenants/{tenantId}/audit-records

*/

/// <summary>
/// Provides the API endpoint that looks up a tenant's audit records in the audit store, newest first, by time range and
/// by the envelope fields an operator investigates with, as CloudTrail's <c>LookupEvents</c> does (ADR-0303).
/// </summary>
internal class AuditRecordApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService
) : BaseApiEndpointHandler, IEndpointProvider
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGroup("/audit-records")
            .WithTags(OpenIdConstants.EndpointTags.AuditRecords)
            .MapGet("", ListAuditRecordsAsync)
            .Produces<CollectionResource<AuditRecordResource>>();
    }

    /// <summary>
    /// Maps a <see cref="PersistedAuditRecord"/> to its <see cref="AuditRecordResource"/> representation.
    /// </summary>
    /// <param name="record">The <see cref="PersistedAuditRecord"/> to map.</param>
    /// <returns>The mapped <see cref="AuditRecordResource"/>.</returns>
    internal virtual AuditRecordResource ToAuditRecordResource(PersistedAuditRecord record) =>
        new()
        {
            EventId = record.EventId,
            EventTime = record.EventTime,
            EventName = record.EventName,
            EventCategory = record.EventCategory,
            Outcome = record.Outcome,
            Severity = record.Severity,
            SubjectId = record.SubjectId,
            ClientId = record.ClientId,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            TraceId = record.TraceId,
            SourceIp = record.SourceIp,
            Resources =
            [
                .. record.Resources.Select(resource => new AuditRecordResourceReference
                {
                    Type = resource.Type,
                    Id = resource.Id,
                }),
            ],
            Record = JsonElement.Parse(record.Record),
        };

    /// <summary>
    /// Lists the audit records of a tenant, newest first.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="startTime">When specified, the earliest event time returned, inclusive.</param>
    /// <param name="endTime">When specified, the latest event time returned, exclusive.</param>
    /// <param name="eventName">When specified, restricts the page to records of this event name.</param>
    /// <param name="eventCategory">When specified, restricts the page to records of this event category.</param>
    /// <param name="outcome">When specified, restricts the page to records with this outcome.</param>
    /// <param name="subjectId">When specified, restricts the page to records about this subject.</param>
    /// <param name="clientId">When specified, restricts the page to records about this client.</param>
    /// <param name="actorId">When specified, restricts the page to records of operations this actor performed.</param>
    /// <param name="resourceType">When specified, restricts the page to records that name a resource of this type.</param>
    /// <param name="resourceId">When specified, restricts the page to records that name a resource with this
    /// identifier, of <paramref name="resourceType"/> when that is specified too.</param>
    /// <param name="traceId">When specified, restricts the page to records of requests with this trace identifier.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of records to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of audit records.</response>
    [EndpointName("api/audit-records/list")]
    internal virtual async ValueTask<IResult> ListAuditRecordsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] DateTimeOffset? startTime,
        [FromQuery] DateTimeOffset? endTime,
        [FromQuery] string? eventName,
        [FromQuery] string? eventCategory,
        [FromQuery] string? outcome,
        [FromQuery] string? subjectId,
        [FromQuery] string? clientId,
        [FromQuery] string? actorId,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] string? traceId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        // Audit records are not tenant-scoped data, so the tenant is a criterion of the lookup, never of a query filter.
        var effectiveLimit = NormalizeLimit(limit);
        var filter = new AuditRecordFilter
        {
            TenantId = tenantId,
            StartTime = startTime,
            EndTime = endTime,
            EventName = NullIfEmpty(eventName),
            EventCategory = NullIfEmpty(eventCategory),
            Outcome = NullIfEmpty(outcome),
            SubjectId = NullIfEmpty(subjectId),
            ClientId = NullIfEmpty(clientId),
            ActorId = NullIfEmpty(actorId),
            ResourceType = NullIfEmpty(resourceType),
            ResourceId = NullIfEmpty(resourceId),
            TraceId = NullIfEmpty(traceId),
        };

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IAuditRecordStore>();
                return await store.GetPageAsync(
                    filter,
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToAuditRecordResource
        );
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

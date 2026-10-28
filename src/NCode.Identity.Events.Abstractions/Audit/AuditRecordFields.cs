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

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the stable, queryable field names of the audit record.
/// </summary>
/// <remarks>
/// These names are the JSON property names of the audit record, so they are a contract with whatever
/// indexes, filters or pivots on them: a log pipeline, a trace backend or a future audit store. Values
/// are lower-case snake_case and never change; a new field is added additively. The nested classes
/// name the fields of the record's object members.
/// </remarks>
[PublicAPI]
public static class AuditRecordFields
{
    /// <summary>
    /// The field name of the version of the audit record's schema.
    /// </summary>
    public const string SchemaVersion = "schema_version";

    /// <summary>
    /// The field name of the unique identifier of the audit event.
    /// </summary>
    public const string EventId = "event_id";

    /// <summary>
    /// The field name of the UTC time at which the audited operation occurred.
    /// </summary>
    public const string EventTime = "event_time";

    /// <summary>
    /// The field name of the name of the audited operation.
    /// </summary>
    public const string EventName = "event_name";

    /// <summary>
    /// The field name of the category of the audit event.
    /// </summary>
    public const string EventCategory = "event_category";

    /// <summary>
    /// The field name of the plane that produced the audit event.
    /// </summary>
    public const string EventSource = "event_source";

    /// <summary>
    /// The field name of the outcome of the audited operation.
    /// </summary>
    public const string Outcome = "outcome";

    /// <summary>
    /// The field name of the severity of the audit event.
    /// </summary>
    public const string Severity = "severity";

    /// <summary>
    /// The field name of the identifier of the tenant the audited operation ran under.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// The field name of the identifier of the subject the audited operation concerns.
    /// </summary>
    public const string SubjectId = "subject_id";

    /// <summary>
    /// The field name of the identifier of the client the audited operation concerns.
    /// </summary>
    public const string ClientId = "client_id";

    /// <summary>
    /// The field name of the object that identifies who performed the audited operation.
    /// </summary>
    public const string Actor = "actor";

    /// <summary>
    /// The field name of the object that describes the request that caused the audited operation.
    /// </summary>
    public const string Request = "request";

    /// <summary>
    /// The field name of the array of entities the audited operation acted on.
    /// </summary>
    public const string Resources = "resources";

    /// <summary>
    /// The field name of the object that describes why the audited operation did not succeed.
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// The field name of the object that holds the fields specific to the audit event.
    /// </summary>
    public const string Details = "details";

    /// <summary>
    /// Provides the field names of the <see cref="Actor"/> object.
    /// </summary>
    [PublicAPI]
    public static class ActorFields
    {
        /// <summary>
        /// The field name of the actor's type.
        /// </summary>
        public const string Type = "type";

        /// <summary>
        /// The field name of the actor's identifier.
        /// </summary>
        public const string Id = "id";
    }

    /// <summary>
    /// Provides the field names of the <see cref="Request"/> object.
    /// </summary>
    [PublicAPI]
    public static class RequestFields
    {
        /// <summary>
        /// The field name of the HTTP request identifier.
        /// </summary>
        public const string RequestId = "request_id";

        /// <summary>
        /// The field name of the W3C trace identifier.
        /// </summary>
        public const string TraceId = "trace_id";

        /// <summary>
        /// The field name of the W3C span identifier.
        /// </summary>
        public const string SpanId = "span_id";

        /// <summary>
        /// The field name of the IP address the request came from.
        /// </summary>
        public const string SourceIp = "source_ip";

        /// <summary>
        /// The field name of the User-Agent of the request.
        /// </summary>
        public const string UserAgent = "user_agent";

        /// <summary>
        /// The field name of the HTTP method of the request.
        /// </summary>
        public const string HttpMethod = "http_method";

        /// <summary>
        /// The field name of the path of the request, without its query string.
        /// </summary>
        public const string Path = "path";

        /// <summary>
        /// The field name of the endpoint that handled the request.
        /// </summary>
        public const string Endpoint = "endpoint";
    }

    /// <summary>
    /// Provides the field names of each object in the <see cref="Resources"/> array.
    /// </summary>
    [PublicAPI]
    public static class ResourceFields
    {
        /// <summary>
        /// The field name of the resource's type.
        /// </summary>
        public const string Type = "type";

        /// <summary>
        /// The field name of the resource's identifier.
        /// </summary>
        public const string Id = "id";
    }

    /// <summary>
    /// Provides the field names of the <see cref="Error"/> object.
    /// </summary>
    [PublicAPI]
    public static class ErrorFields
    {
        /// <summary>
        /// The field name of the stable machine-readable error code.
        /// </summary>
        public const string Code = "code";

        /// <summary>
        /// The field name of the human-readable error message.
        /// </summary>
        public const string Message = "message";

        /// <summary>
        /// The field name of the HTTP status code associated with the error.
        /// </summary>
        public const string StatusCode = "status_code";
    }
}

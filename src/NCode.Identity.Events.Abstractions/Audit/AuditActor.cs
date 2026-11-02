// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Describes the caller that performed an audited operation.
/// </summary>
/// <param name="Type">
/// The kind of caller, typically one of the <see cref="AuditActorTypes"/> codes.
/// </param>
/// <param name="Id">
/// The identifier of the caller within its <paramref name="Type"/> (for example a subject identifier
/// or a client identifier), or <see langword="null"/> when the caller has no identifier.
/// </param>
[PublicAPI]
public sealed record AuditActor(string Type, string? Id = null)
{
    /// <summary>
    /// Gets the actor for a caller that has not been identified.
    /// </summary>
    public static AuditActor Anonymous { get; } = new(AuditActorTypes.Anonymous);

    /// <summary>
    /// Gets the actor for the system itself acting without a caller.
    /// </summary>
    public static AuditActor System { get; } = new(AuditActorTypes.System);

    /// <summary>
    /// Creates the actor for an end user, or <see cref="Anonymous"/> when there is no subject.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the end user, if known.</param>
    public static AuditActor EndUser(string? subjectId) =>
        subjectId is null ? Anonymous : new AuditActor(AuditActorTypes.EndUser, subjectId);

    /// <summary>
    /// Creates the actor for an OAuth client.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    public static AuditActor Client(string clientId) => new(AuditActorTypes.Client, clientId);

    /// <summary>
    /// Creates the actor for an administrator of the management plane.
    /// </summary>
    /// <param name="administratorId">The identifier of the administrator, if known.</param>
    public static AuditActor Administrator(string? administratorId) =>
        new(AuditActorTypes.Administrator, administratorId);
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Contains the <see cref="OperationAuthorizationRequirement"/> instances for the standard CRUD operations
/// used by the OpenID management API endpoints.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class Operations
{
    /// <summary>
    /// Gets the authorization requirement for the <c>Create</c> operation.
    /// </summary>
    public static OperationAuthorizationRequirement Create { get; } =
        new() { Name = nameof(Create) };

    /// <summary>
    /// Gets the authorization requirement for the <c>Read</c> operation.
    /// </summary>
    public static OperationAuthorizationRequirement Read { get; } = new() { Name = nameof(Read) };

    /// <summary>
    /// Gets the authorization requirement for the <c>List</c> operation (enumerating a resource family).
    /// </summary>
    public static OperationAuthorizationRequirement List { get; } = new() { Name = nameof(List) };

    /// <summary>
    /// Gets the authorization requirement for the <c>Update</c> operation.
    /// </summary>
    public static OperationAuthorizationRequirement Update { get; } =
        new() { Name = nameof(Update) };

    /// <summary>
    /// Gets the authorization requirement for the <c>Delete</c> operation.
    /// </summary>
    public static OperationAuthorizationRequirement Delete { get; } =
        new() { Name = nameof(Delete) };
}

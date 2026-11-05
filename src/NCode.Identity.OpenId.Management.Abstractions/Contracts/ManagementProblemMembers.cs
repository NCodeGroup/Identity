// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Contains the names of the extension members that the management API adds to a problem details response.
/// </summary>
[PublicAPI]
public static class ManagementProblemMembers
{
    /// <summary>
    /// The number of whole seconds that the caller is asked to wait, the same value as the <c>Retry-After</c> header.
    /// </summary>
    public const string RetryAfterSeconds = "retryAfterSeconds";

    /// <summary>
    /// The type of the owner that the problem concerns.
    /// </summary>
    public const string OwnerType = "ownerType";

    /// <summary>
    /// The identifier of the owner that the problem concerns.
    /// </summary>
    public const string OwnerId = "ownerId";

    /// <summary>
    /// The name of the key provider that the problem concerns.
    /// </summary>
    public const string Provider = "provider";

    /// <summary>
    /// The reference of the key that the problem concerns.
    /// </summary>
    public const string KeyReference = "keyReference";

    /// <summary>
    /// The settings scope that the problem concerns.
    /// </summary>
    public const string SettingsScope = "settingsScope";

    /// <summary>
    /// The identity that reaches the key that the problem concerns.
    /// </summary>
    public const string AccessIdentity = "accessIdentity";

    /// <summary>
    /// The description of the document error that the problem concerns.
    /// </summary>
    public const string DocumentError = "documentError";
}

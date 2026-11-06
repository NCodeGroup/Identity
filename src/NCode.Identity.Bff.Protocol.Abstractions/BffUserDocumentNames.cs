// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The names of the properties of the document that <c>GET /bff/user</c> answers, and the key under which a Blazor
/// server persists the document for its WebAssembly client.
/// </summary>
[PublicAPI]
public static class BffUserDocumentNames
{
    /// <summary>
    /// The name of the property that holds the subject of the user.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    /// The name of the property that holds the display name of the user.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    /// The name of the property that holds the claims of the user.
    /// </summary>
    public const string Claims = "claims";

    /// <summary>
    /// The name of the property that holds the session of the user.
    /// </summary>
    public const string Session = "session";

    /// <summary>
    /// The name of the property that holds the time at which the session expires.
    /// </summary>
    public const string ExpiresAt = "expiresAt";

    /// <summary>
    /// The name of the property that holds the time at which the session expires when it stays idle.
    /// </summary>
    public const string IdleExpiresAt = "idleExpiresAt";

    /// <summary>
    /// The name of the property that holds the URL that signs the user out.
    /// </summary>
    public const string LogoutUrl = "logoutUrl";

    /// <summary>
    /// The name of the property that holds the session identifier that the identity provider uses to sign the user
    /// out.
    /// </summary>
    public const string LogoutSid = "logoutSid";

    /// <summary>
    /// The key under which a Blazor server persists the document for its WebAssembly client.
    /// </summary>
    public const string PersistedStateKey = "NCode.Identity.Bff.User";
}

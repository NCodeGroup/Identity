// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Provides the names the backend-for-frontend (BFF) Blazor WebAssembly client uses.
/// </summary>
[PublicAPI]
public static class BffBlazorClientDefaults
{
    /// <summary>
    /// The key under which the server persists the <c>/bff/user</c> document for the WebAssembly client.
    /// </summary>
    public const string UserStateKey = "NCode.Identity.Bff.User";

    /// <summary>
    /// The authentication type of the <see cref="System.Security.Claims.ClaimsIdentity"/> built from the user document.
    /// </summary>
    public const string AuthenticationType = "NCode.Identity.Bff";

    /// <summary>
    /// The name of the <see cref="System.Net.Http.HttpClient"/> that calls the BFF's own endpoints.
    /// </summary>
    public const string HttpClientName = "NCode.Identity.Bff.Blazor.Client";
}

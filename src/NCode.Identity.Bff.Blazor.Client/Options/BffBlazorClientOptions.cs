// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Blazor.Client.Options;

/// <summary>
/// Configures how the backend-for-frontend (BFF) Blazor WebAssembly client reaches the BFF's own endpoints. The
/// frontend's <c>PathPrefix</c> and the deployment's <c>Bff:Antiforgery</c> settings must match these values.
/// </summary>
[PublicAPI]
public sealed class BffBlazorClientOptions
{
    /// <summary>
    /// Gets or sets the same-origin path of the BFF's user endpoint. The default is <c>/bff/user</c>.
    /// </summary>
    public string UserPath { get; set; } = BffDefaults.EndpointPrefix + BffEndpointPaths.User;

    /// <summary>
    /// Gets or sets the name of the anti-forgery header the client sends. The default is <c>X-CSRF</c>.
    /// </summary>
    public string AntiforgeryHeaderName { get; set; } = BffDefaults.AntiforgeryHeaderName;

    /// <summary>
    /// Gets or sets the value of the anti-forgery header the client sends. The default is <c>1</c>.
    /// </summary>
    public string AntiforgeryHeaderValue { get; set; } = BffDefaults.AntiforgeryHeaderValue;

    /// <summary>
    /// Gets or sets how often the client probes the session. The default is 1 minute.
    /// </summary>
    public TimeSpan ProbeInterval { get; set; } = TimeSpan.FromMinutes(1);
}

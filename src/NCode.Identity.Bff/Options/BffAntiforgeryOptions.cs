// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the static anti-forgery header that every request to a BFF-protected endpoint must carry.
/// </summary>
[PublicAPI]
public sealed class BffAntiforgeryOptions
{
    /// <summary>
    /// Gets or sets the name of the required request header. The default is <c>X-CSRF</c>. The configuration key is
    /// <c>Bff:Antiforgery:HeaderName</c>.
    /// </summary>
    public string HeaderName { get; set; } = BffDefaults.AntiforgeryHeaderName;

    /// <summary>
    /// Gets or sets the required value of the request header. The default is <c>1</c>. The configuration key is
    /// <c>Bff:Antiforgery:HeaderValue</c>.
    /// </summary>
    public string HeaderValue { get; set; } = BffDefaults.AntiforgeryHeaderValue;
}

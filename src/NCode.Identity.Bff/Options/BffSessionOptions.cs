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
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the lifetime and cookie of the BFF session.
/// </summary>
[PublicAPI]
public sealed class BffSessionOptions
{
    /// <summary>
    /// Gets or sets the longest a session can last from login. The default is 8 hours. The configuration key is
    /// <c>Bff:Session:AbsoluteLifetime</c>.
    /// </summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets how long a session survives without a request to a BFF API. The default is 1 hour. The
    /// configuration key is <c>Bff:Session:IdleTimeout</c>.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the <c>SameSite</c> mode of the session cookie. The default is <see cref="SameSiteMode.Strict"/>.
    /// The configuration key is <c>Bff:Session:SameSite</c>.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;

    /// <summary>
    /// Gets or sets how long a login attempt can take, from the redirect to the callback. The default is 10 minutes.
    /// The configuration key is <c>Bff:Session:LoginLifetime</c>.
    /// </summary>
    public TimeSpan LoginLifetime { get; set; } = TimeSpan.FromMinutes(10);
}

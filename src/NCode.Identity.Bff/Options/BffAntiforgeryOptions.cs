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

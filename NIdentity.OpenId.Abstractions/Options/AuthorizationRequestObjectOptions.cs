#region Copyright Preamble
// 
//    Copyright @ 2023 NCode Group
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

namespace NIdentity.OpenId.Options;

/// <summary>
/// Contains configurable options for dealing with request objects in the <c>OpenID Connect</c> authorization handler.
/// </summary>
public class AuthorizationRequestObjectOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the <c>request</c> parameter in an <c>OpenID Connect</c>
    /// authorization message is supported or not.
    /// </summary>
    public bool RequestJwtEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>request_uri</c> parameter in an <c>OpenID Connect</c>
    /// authorization message is supported or not.
    /// </summary>
    public bool RequestUriEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowable length for the <c>request_uri</c> parameter in an <c>OpenID Connect</c>
    /// authorization message. The default maximum length is 512.
    /// </summary>
    public int RequestUriMaxLength { get; set; } = 512;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>Content-Type</c> returned from fetching the <c>request_uri</c>
    /// must match the value configured in <see cref="ExpectedContentType"/>.
    /// </summary>
    public bool StrictContentType { get; set; }

    /// <summary>
    /// Gets or sets the expected <c>Content-Type</c> that should be returned from <c>request_uri</c>. Defaults to
    /// <c>application/oauth-authz-req+jwt</c>. Only used if <see cref="StrictContentType"/> is <c>true</c>.
    /// </summary>
    public string ExpectedContentType { get; set; } = "application/oauth-authz-req+jwt";

    /// <summary>
    /// Gets or sets the value to use when validating the <c>audience</c> JWT claim when fetching the request object
    /// from <c>request_uri</c>.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}

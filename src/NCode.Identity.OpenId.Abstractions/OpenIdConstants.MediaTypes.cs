#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the media types used when rendering an OpenID response. A response formatter
    /// (<c>IOpenIdResponseFormatter</c>) declares the media types it emits using these values.
    /// </summary>
    public static class MediaTypes
    {
        /// <summary>
        /// The <c>application/json</c> media type — the default OAuth 2.0 / OpenID Connect response representation
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc6749#section-5.1">RFC 6749 §5.1</see> requires token and
        /// error responses be <c>application/json</c>).
        /// </summary>
        public const string Json = "application/json";

        /// <summary>
        /// The <c>text/html</c> media type — an interactive (browser) representation, for example the non-redirectable
        /// authorization error page.
        /// </summary>
        public const string Html = "text/html";
    }
}

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
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the extension method that writes the security headers of an HTML page. Every page that the server renders
/// is written through it, so no page is served without a Content-Security-Policy or in a frame that it did not allow.
/// </summary>
[PublicAPI]
public static class HtmlPageSecurityResponseExtensions
{
    private const string ReferrerPolicyHeaderName = "Referrer-Policy";

    /// <param name="response">The <see cref="HttpResponse"/> instance.</param>
    extension(HttpResponse response)
    {
        /// <summary>
        /// Sets the security headers of an HTML page: <c>Content-Security-Policy</c>, <c>X-Frame-Options</c>,
        /// <c>X-Content-Type-Options</c>, <c>Referrer-Policy</c>, <c>Cache-Control</c> and <c>Pragma</c>. The headers
        /// are assigned, so a value that another component wrote, such as the <c>SAMEORIGIN</c> frame option that
        /// antiforgery adds, is replaced. When <see cref="HtmlPageSecurityPolicy.AllowsFraming"/> is set or
        /// <see cref="HtmlPageSecurityPolicy.FrameAncestors"/> lists a source, the response carries no
        /// <c>X-Frame-Options</c> header, because that header cannot name an origin; otherwise it is <c>DENY</c>.
        /// </summary>
        /// <param name="policy">The <see cref="HtmlPageSecurityPolicy"/> that describes the page.</param>
        public void SetHtmlPageSecurity(HtmlPageSecurityPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(policy);

            var headers = response.Headers;

            headers.ContentSecurityPolicy = policy.ToContentSecurityPolicy();

            if (policy.AllowsFraming || !policy.FrameAncestors.IsDefaultOrEmpty)
            {
                headers.Remove(HeaderNames.XFrameOptions);
            }
            else
            {
                headers.XFrameOptions = "DENY";
            }

            headers.XContentTypeOptions = "nosniff";
            headers[ReferrerPolicyHeaderName] = "same-origin";
            headers.CacheControl = "no-store, no-cache, max-age=0";
            headers.Pragma = "no-cache";
        }
    }
}

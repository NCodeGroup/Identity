// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods for <see cref="HttpResponse"/>.
/// </summary>
internal static class HttpResponseExtensions
{
    extension(HttpResponse response)
    {
        /// <summary>
        /// Marks the response as non-cacheable so a sensitive payload (one carrying tokens, codes, or other secrets)
        /// is never stored by the client or an intermediary: emits <c>Cache-Control: no-store, no-cache, max-age=0</c>
        /// and the legacy <c>Pragma: no-cache</c>. The token endpoint's <c>no-store</c> is mandated by RFC 6749
        /// §5.1/§5.2; the authorization response carries the same headers.
        /// </summary>
        public void SetNoStore()
        {
            response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            response.Headers.Pragma = "no-cache";
        }
    }
}

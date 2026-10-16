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

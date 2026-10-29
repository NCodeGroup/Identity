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

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the access token of the <c>user</c> token requirement from the session of a request.
/// </summary>
internal interface IBffUserTokenService
{
    /// <summary>
    /// Gets an access token for the user of the request, refreshing it when it is stale.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="resource">The resource indicator, or <see langword="null"/>.</param>
    /// <param name="scope">The scope, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        string? resource,
        string? scope,
        CancellationToken cancellationToken
    );
}

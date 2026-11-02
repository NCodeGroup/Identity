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

using Polly.Timeout;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Turns the <see cref="TimeoutRejectedException"/> that a resilience timeout throws into the
/// <see cref="TaskCanceledException"/> with a <see cref="TimeoutException"/> inner exception that
/// <see cref="HttpClient.Timeout"/> produces, so a caller that catches <see cref="HttpRequestException"/> or
/// <see cref="OperationCanceledException"/> does not let a timeout escape.
/// </summary>
// See ADR-1315.
internal sealed class GuardedHttpTimeoutHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (TimeoutRejectedException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException(
                "The outbound HTTP call timed out.",
                new TimeoutException(exception.Message, exception)
            );
        }
    }
}

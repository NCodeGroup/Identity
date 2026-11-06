// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.EndToEnd.BlazorApp;

/// <summary>
/// Counts what the Blazor host saw, so that a test can assert it beside the browser's outcome.
/// </summary>
public sealed class HostCounters
{
    private int _hubRequests;
    private int _apiCalls;
    private int _tokenHeaderRequests;

    /// <summary>
    /// Gets the number of requests the host received for the Blazor hub.
    /// </summary>
    public int HubRequests => Volatile.Read(ref _hubRequests);

    /// <summary>
    /// Gets the number of calls the host received for its API endpoint.
    /// </summary>
    public int ApiCalls => Volatile.Read(ref _apiCalls);

    /// <summary>
    /// Gets the number of API calls that carried an <c>Authorization</c> or <c>DPoP</c> header.
    /// </summary>
    public int TokenHeaderRequests => Volatile.Read(ref _tokenHeaderRequests);

    /// <summary>
    /// Counts a request for the Blazor hub.
    /// </summary>
    public void CountHubRequest()
    {
        Interlocked.Increment(ref _hubRequests);
    }

    /// <summary>
    /// Counts a call to the API endpoint, and whether it carried a token header.
    /// </summary>
    /// <param name="headers">The headers of the request.</param>
    public void CountApiCall(IHeaderDictionary headers)
    {
        Interlocked.Increment(ref _apiCalls);
        if (headers.ContainsKey("Authorization") || headers.ContainsKey("DPoP"))
        {
            Interlocked.Increment(ref _tokenHeaderRequests);
        }
    }
}

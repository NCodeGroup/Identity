// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// One mapped remote API: the local path prefix, the destination it is forwarded to, and how the forwarder obtains
/// its token and sends the request.
/// </summary>
/// <param name="path">The local path prefix.</param>
/// <param name="destination">The absolute base URL that requests are forwarded to.</param>
/// <param name="tokenRequest">The token request that each forwarded request carries the result of.</param>
/// <param name="allowedMethods">The accepted HTTP methods, or an empty collection to accept every method.</param>
/// <param name="timeout">How long the forwarder waits for upstream activity.</param>
/// <param name="invoker">The invoker that sends the forwarded requests.</param>
internal sealed class BffRemoteApi(
    string path,
    string destination,
    BffTokenRequest tokenRequest,
    IEnumerable<string> allowedMethods,
    TimeSpan timeout,
    HttpMessageInvoker invoker
)
{
    /// <summary>
    /// Gets the local path prefix.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the absolute base URL that requests are forwarded to.
    /// </summary>
    public string Destination { get; } = destination;

    /// <summary>
    /// Gets the token request that each forwarded request carries the result of.
    /// </summary>
    public BffTokenRequest TokenRequest { get; } = tokenRequest;

    /// <summary>
    /// Gets the accepted HTTP methods, or an empty set to accept every method.
    /// </summary>
    public FrozenSet<string> AllowedMethods { get; } =
        allowedMethods
            .Select(method => method.ToUpperInvariant())
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the value of the <c>Allow</c> header that a refused method is answered with.
    /// </summary>
    public string AllowHeader => string.Join(", ", AllowedMethods.Order(StringComparer.Ordinal));

    /// <summary>
    /// Gets the configuration of each forwarded request.
    /// </summary>
    public ForwarderRequestConfig RequestConfig { get; } = new() { ActivityTimeout = timeout };

    /// <summary>
    /// Gets the invoker that sends the forwarded requests.
    /// </summary>
    public HttpMessageInvoker Invoker { get; } = invoker;

    /// <summary>
    /// Gets whether the remote API accepts the specified HTTP method.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <returns><c>true</c> when the method is accepted.</returns>
    public bool AllowsMethod(string method) =>
        AllowedMethods.Count == 0 || AllowedMethods.Contains(method);
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Sockets;
using System.Security.Authentication;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Contains the names of the transport causes that stop a fetch through the guarded outbound client.
/// </summary>
/// <remarks>
/// The reasons are a closed vocabulary (ADR-1150): each is one way the guarded client's send can fail. Names compare
/// ordinally.
/// </remarks>
// See ADR-1315.
internal static class OutboundFetchFailureReasons
{
    /// <summary>
    /// The outbound guard refused every address the host resolves to.
    /// </summary>
    public const string BlockedAddress = "blocked_address";

    /// <summary>
    /// The host name did not resolve.
    /// </summary>
    public const string NameResolution = "name_resolution";

    /// <summary>
    /// The TLS handshake with the host failed.
    /// </summary>
    public const string Tls = "tls";

    /// <summary>
    /// The fetch did not complete in time.
    /// </summary>
    public const string Timeout = "timeout";

    /// <summary>
    /// The fetch was canceled.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// The connection to the host failed for a cause that no other reason names.
    /// </summary>
    public const string Connection = "connection";

    /// <summary>
    /// The host answered with a status code that is not a success.
    /// </summary>
    public const string HttpStatus = "http_status";

    /// <summary>
    /// Names the cause of a failed fetch.
    /// </summary>
    /// <param name="exception">The exception that the guarded client's send threw.</param>
    /// <param name="timedOut">
    /// <see langword="true"/> when the fetch's own timeout, not the caller, canceled it.
    /// </param>
    /// <returns>The name of the reason that best describes <paramref name="exception"/>.</returns>
    public static string Classify(Exception exception, bool timedOut)
    {
        // The guard's refusal, a TLS failure and a name-resolution failure are found by walking the exception chain,
        // because SocketsHttpHandler wraps whatever its connect callback throws.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case OutboundAddressRefusedException:
                    return BlockedAddress;

                case AuthenticationException:
                    return Tls;

                case TimeoutException:
                    return Timeout;

                case SocketException
                {
                    SocketErrorCode: SocketError.HostNotFound
                        or SocketError.NoData
                        or SocketError.TryAgain
                }:
                case HttpRequestException
                {
                    HttpRequestError: HttpRequestError.NameResolutionError
                }:
                    return NameResolution;

                case HttpRequestException { StatusCode: not null }:
                    return HttpStatus;
            }
        }

        if (timedOut)
            return Timeout;

        return exception is OperationCanceledException ? Canceled : Connection;
    }
}

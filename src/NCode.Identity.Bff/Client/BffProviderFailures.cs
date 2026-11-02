// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Client;

/// <summary>
/// Classifies the exceptions that mean the OpenID provider could not be reached or read, so every BFF call to the
/// provider answers them the same way.
/// </summary>
internal static class BffProviderFailures
{
    /// <summary>
    /// Determines whether an exception means the provider is unavailable: its metadata or keys could not be read, the
    /// request failed in transport, or it timed out without the caller canceling.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="cancellationToken">The caller's <see cref="CancellationToken"/>.</param>
    /// <returns><see langword="true"/> when the provider is unavailable; otherwise, <see langword="false"/>.</returns>
    public static bool IsUnavailable(Exception exception, CancellationToken cancellationToken) =>
        exception is OpenIdProviderException or HttpRequestException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}

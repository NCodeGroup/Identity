// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Names the cause of a failed outbound delivery and hands the redacted URL and the reason to the caller's
/// log call.
/// </summary>
internal static class OutboundDeliveryFailureLog
{
    /// <summary>
    /// Names the cause of a failed outbound delivery and passes it to the caller's log call.
    /// </summary>
    /// <param name="uri">The URI that the delivery targeted.</param>
    /// <param name="exception">The exception that failed the delivery.</param>
    /// <param name="log">
    /// The caller's log call. It receives the URI without its user information, query or fragment, then the
    /// reason that names the cause of the failure.
    /// </param>
    public static void Report(string uri, Exception exception, Action<string, string> log)
    {
        // A delivery has no timeout source of its own: the guarded client's resilience timeout surfaces as a
        // TimeoutException in the exception chain, which Classify names.
        log(
            OutboundUriRedaction.ForLog(uri),
            OutboundFetchFailureReasons.Classify(exception, timedOut: false)
        );
    }
}

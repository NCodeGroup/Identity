// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms;

/// <summary>
/// Contains the rules for the email address of a Google Cloud service account, which the wrapper impersonates to reach a
/// Cloud KMS crypto key that the holder of the key grants the service account access to.
/// </summary>
// See ADR-1429.
internal static partial class GoogleServiceAccountEmail
{
    // '\z' rather than '$', which also matches before a trailing newline, and ASCII classes.
    [GeneratedRegex(
        @"^[a-z][a-z0-9-]{4,28}[a-z0-9]@[a-z0-9][a-z0-9.-]*\.iam\.gserviceaccount\.com\z"
    )]
    private static partial Regex Pattern();

    /// <summary>
    /// Determines whether the specified value is the email address of a service account.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>
    /// <see langword="true"/> if the value is a service account email address; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && Pattern().IsMatch(value);
}

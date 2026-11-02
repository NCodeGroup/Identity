// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms;

/// <summary>
/// Contains the rules for the ARN of an AWS KMS key or alias: whether a value is one, and which region it names.
/// </summary>
// See ADR-1235.
internal static partial class AwsKmsKeyArn
{
    // '\z' rather than '$', which also matches before a trailing newline, and '[0-9]' rather than '\d', which accepts
    // any Unicode digit.
    [GeneratedRegex(
        @"^arn:aws[a-z-]*:kms:[a-z0-9-]+:[0-9]{12}:(key/[A-Za-z0-9-]+|alias/[A-Za-z0-9/_-]+)\z"
    )]
    private static partial Regex Pattern();

    /// <summary>
    /// Determines whether the specified value is the ARN of a KMS key or of a KMS alias.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a KMS key or alias ARN; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && Pattern().IsMatch(value);

    /// <summary>
    /// Gets the region that the specified ARN names. The ARN must already have been validated with
    /// <see cref="IsValid"/>.
    /// </summary>
    /// <param name="keyArn">The ARN of a KMS key or alias.</param>
    /// <returns>The region of the ARN, such as <c>us-west-2</c>.</returns>
    public static string GetRegion(string keyArn)
    {
        // The region is the fourth ':'-separated field; step past three separators to find where it starts.
        var start = 0;
        for (var count = 0; count < 3; ++count)
        {
            start = keyArn.IndexOf(':', start) + 1;
        }

        var end = keyArn.IndexOf(':', start);
        return keyArn.Substring(start, end - start);
    }
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms;

/// <summary>
/// Contains the rules for the ARN of an AWS IAM role, which the wrapper assumes to reach a KMS key that the holder of
/// the key grants the role access to.
/// </summary>
// See ADR-1429.
internal static partial class AwsIamRoleArn
{
    // '\z' rather than '$', which also matches before a trailing newline, and ASCII classes rather than '\d' and
    // '\w', which accept any Unicode digit or letter.
    [GeneratedRegex(@"^arn:aws[a-z-]*:iam::[0-9]{12}:role/[A-Za-z0-9_+=,.@/-]{1,576}\z")]
    private static partial Regex Pattern();

    /// <summary>
    /// Determines whether the specified value is the ARN of an IAM role.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is an IAM role ARN; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && Pattern().IsMatch(value);
}

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

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.Persistence.AwsKms;

/// <summary>
/// Contains the rules for the ARN of an AWS KMS key or alias: whether a value is one, and which region it names.
/// </summary>
// See ADR-0235.
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

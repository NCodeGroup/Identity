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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Describes the judgment of an <see cref="IPasswordPolicy"/> on a password: it is accepted, or it is rejected with an
/// error code and a description of the requirement it does not meet.
/// </summary>
// See ADR-0236.
[PublicAPI]
public sealed class PasswordPolicyResult
{
    /// <summary>
    /// Gets the result for a password that the policy accepts.
    /// </summary>
    public static PasswordPolicyResult Accepted { get; } = new(null, null);

    /// <summary>
    /// Creates the result for a password that the policy rejects.
    /// </summary>
    /// <param name="error">
    /// The code that names the reason, one of the <see cref="PasswordPolicyErrors"/> or a code of a replacement policy.
    /// </param>
    /// <param name="errorDescription">The description of the requirement the password does not meet.</param>
    /// <returns>The <see cref="PasswordPolicyResult"/> that is rejected.</returns>
    public static PasswordPolicyResult Rejected(string error, string errorDescription)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        ArgumentException.ThrowIfNullOrEmpty(errorDescription);
        return new PasswordPolicyResult(error, errorDescription);
    }

    private PasswordPolicyResult(string? error, string? errorDescription)
    {
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>
    /// Gets the code that names the reason the password is rejected, or <see langword="null"/> when it is accepted.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the description of the requirement the password does not meet, or <see langword="null"/> when it is
    /// accepted.
    /// </summary>
    public string? ErrorDescription { get; }

    /// <summary>
    /// Gets a value indicating whether the policy rejects the password.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error), nameof(ErrorDescription))]
    public bool IsRejected => Error is not null && ErrorDescription is not null;
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Describes the outcome of a change of the signed-in end user's password: the password is changed, or the change is
/// refused with the error code the change-password page shows.
/// </summary>
// See ADR-1230.
[PublicAPI]
public sealed class InteractivePasswordChangeResult
{
    /// <summary>
    /// Gets the result for a password that is changed.
    /// </summary>
    public static InteractivePasswordChangeResult Changed { get; } = new(null);

    /// <summary>
    /// Gets the result for a backend that has no password to change for the end user.
    /// </summary>
    public static InteractivePasswordChangeResult NotSupported { get; } =
        new(InteractivePasswordChangeErrors.NotSupported);

    /// <summary>
    /// Gets the result for a current password that is not valid, so nothing is changed.
    /// </summary>
    public static InteractivePasswordChangeResult CurrentPasswordInvalid { get; } =
        new(InteractivePasswordChangeErrors.InvalidCurrentPassword);

    /// <summary>
    /// Creates the result for a change that the backend refuses, so nothing is changed.
    /// </summary>
    /// <param name="error">
    /// The code that names the reason, one of the <see cref="InteractivePasswordChangeErrors"/> or a code of the
    /// backend, such as a code of the password policy.
    /// </param>
    /// <returns>The <see cref="InteractivePasswordChangeResult"/> that is refused with the error code.</returns>
    public static InteractivePasswordChangeResult Refused(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        return new InteractivePasswordChangeResult(error);
    }

    private InteractivePasswordChangeResult(string? error) => Error = error;

    /// <summary>
    /// Gets the code that names the reason the change is refused, or <see langword="null"/> when the password is
    /// changed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the password is changed.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsChanged => Error is null;
}

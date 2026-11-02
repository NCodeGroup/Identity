// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Contains the known error codes the change-password page receives in its <c>error</c> query parameter. The
/// vocabulary is open: a credential backend (<see cref="IInteractivePasswordChanger"/>) may report a code of its own,
/// such as the password policy's <c>password_too_short</c>, <c>password_too_long</c> or <c>password_blocklisted</c>,
/// and the page shows a general message for a code it does not know.
/// </summary>
// See ADR-1230.
[PublicAPI]
public static class InteractivePasswordChangeErrors
{
    /// <summary>
    /// Contains the code for a request that lacks the current or the new password.
    /// </summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>
    /// Contains the code for a request whose confirmation differs from the new password.
    /// </summary>
    public const string PasswordMismatch = "password_mismatch";

    /// <summary>
    /// Contains the code for a request whose current password is not valid.
    /// </summary>
    public const string InvalidCurrentPassword = "invalid_current_password";

    /// <summary>
    /// Contains the code for a credential backend that has no password to change for the end user.
    /// </summary>
    public const string NotSupported = "not_supported";
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;

/// <summary>
/// Generates and normalizes the human-typable <c>user_code</c> of a device authorization request (RFC 8628 §6.1).
/// Applications replace the default registration to change the code alphabet, length policy, or display formatting.
/// </summary>
[PublicAPI]
public interface IUserCodeGenerator
{
    /// <summary>
    /// Generates a new <c>user_code</c> of the specified length, formatted for display.
    /// </summary>
    /// <param name="length">The number of significant characters in the generated code.</param>
    /// <returns>The generated <c>user_code</c>, formatted for display (for example <c>WDJB-MJHT</c>).</returns>
    string Generate(int length);

    /// <summary>
    /// Normalizes a user-supplied <c>user_code</c> for lookup by upper-casing it and stripping any character that is
    /// not part of the code alphabet (such as spaces or separators the user may have typed).
    /// </summary>
    /// <param name="userCode">The user-supplied <c>user_code</c>.</param>
    /// <returns>The normalized <c>user_code</c>.</returns>
    string Normalize(string userCode);
}

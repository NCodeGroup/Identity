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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;

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

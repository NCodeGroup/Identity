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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// The known machine-readable codes of an invalid UserInfo response.
/// </summary>
/// <remarks>
/// The codes are an open vocabulary, compared ordinally.
/// </remarks>
[PublicAPI]
public static class UserInfoFailureCodes
{
    /// <summary>
    /// The response is about another subject than the ID token (OpenID Connect Core 1.0 §5.3.2), so its claims must
    /// not be used.
    /// </summary>
    public const string SubjectMismatch = TokenValidationFailureCodes.SubjectMismatch;

    /// <summary>
    /// The response is not a JSON object with a string <c>sub</c>.
    /// </summary>
    public const string InvalidResponse = "invalid_response";

    /// <summary>
    /// The response is a signed or encrypted JWT (<c>application/jwt</c>), which the client does not validate.
    /// </summary>
    public const string UnsupportedResponse = "unsupported_response";
}

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

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Describes the outcome of verifying a password against a stored hash.
/// </summary>
[PublicAPI]
public enum PasswordVerificationResult
{
    /// <summary>
    /// The password did not match the stored hash.
    /// </summary>
    Failed = 0,

    /// <summary>
    /// The password matched the stored hash.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The password matched, but the stored hash uses weaker parameters than the current configuration and should be
    /// rehashed and re-stored.
    /// </summary>
    SuccessRehashNeeded = 2,
}

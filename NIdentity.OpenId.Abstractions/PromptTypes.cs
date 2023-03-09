#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

namespace NIdentity.OpenId;

/// <summary>
/// Specifies whether the Authorization Server prompts the End-User for reauthentication and consent. This parameter
/// can be used by the Client to make sure that the End-User is still present for the current session or to bring
/// attention to the request. If this parameter contains <c>none</c> with any other value, an error is returned.
/// </summary>
[Flags]
public enum PromptTypes
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The Authorization Server MUST NOT display any authentication or consent user interface pages. An error is
    /// returned if an End-User is not already authenticated or the Client does not have pre-configured consent for
    /// the requested Claims or does not fulfill other conditions for processing the request. The error code will
    /// typically be <c>login_required</c>, <c>interaction_required</c>, or another code defined in Section 3.1.2.6.
    /// This can be used as a method to check for existing authentication and/or consent.
    /// </summary>
    None = 1,

    /// <summary>
    /// The Authorization Server SHOULD prompt the End-User for reauthentication. If it cannot reauthenticate the
    /// End-User, it MUST return an error, typically <c>login_required</c>.
    /// </summary>
    Login = 2,

    /// <summary>
    /// The Authorization Server SHOULD prompt the End-User for consent before returning information to the Client.
    /// If it cannot obtain consent, it MUST return an error, typically <c>consent_required</c>.
    /// </summary>
    Consent = 4,

    /// <summary>
    /// The Authorization Server SHOULD prompt the End-User to select a user account. This enables an End-User who
    /// has multiple accounts at the Authorization Server to select amongst the multiple accounts that they might
    /// have current sessions for. If it cannot obtain an account selection choice made by the End-User, it MUST
    /// return an error, typically <c>account_selection_required</c>.
    /// </summary>
    SelectAccount = 8,

    /// <summary>
    /// TODO
    /// </summary>
    CreateAccount = 16,
}

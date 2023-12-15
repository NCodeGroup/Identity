#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using System.Text.Json;
using NCode.Jose.SecretKeys;

namespace NIdentity.OpenId.Clients;

public abstract class OpenIdAuthenticatedClient : OpenIdClient
{
    /// <summary>
    /// Gets a <see cref="string"/> value containing the authentication method used to authenticate the client.
    /// </summary>
    public abstract string AuthenticationMethod { get; }

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to authenticate the client.
    /// </summary>
    public abstract SecretKey SecretKey { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the client was authenticated using a proof-of-possession key.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Confirmation))]
    public bool HasConfirmation => Confirmation != null;

    /// <summary>
    /// Gets the JSON payload containing the identifier for the proof-of-possession key used to authenticate the client.
    /// </summary>
    public abstract JsonElement? Confirmation { get; }
}

#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides contextual information about redirects and responses can be safely returned to the user-agent.
/// </summary>
public readonly struct ClientRedirectContext
{
    /// <summary>
    /// Gets or sets the <c>state</c> parameter that should be included when returning responses to the user-agent.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="Client"/> instance associated with the authorization request.
    /// </summary>
    public required Client Client { get; init; }

    /// <summary>
    /// Gets the <see cref="IKnownSettingCollection"/> that contains client settings merged with tenant settings.
    /// </summary>
    public required IKnownSettingCollection ClientSettings { get; init; }

    /// <summary>
    /// Gets or sets the <c>redirect_uri</c> that should be used to return responses to the user-agent.
    /// </summary>
    public required Uri RedirectUri { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="ResponseMode"/> that should be used to return responses to the user-agent.
    /// </summary>
    public required ResponseMode ResponseMode { get; init; }
}

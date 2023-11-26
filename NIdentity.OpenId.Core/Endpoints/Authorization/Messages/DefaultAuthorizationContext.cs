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

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="AuthorizationContext"/> abstraction.
/// </summary>
public class DefaultAuthorizationContext(
    OpenIdContext openIdContext,
    Client client,
    IKnownSettingCollection clientSettings,
    IAuthorizationRequest authorizationRequest,
    bool isContinuation
) : AuthorizationContext
{
    /// <inheritdoc />
    public override OpenIdContext OpenIdContext { get; } = openIdContext;

    /// <inheritdoc />
    public override Client Client { get; } = client;

    /// <inheritdoc />
    public override IKnownSettingCollection ClientSettings { get; } = clientSettings;

    /// <inheritdoc />
    public override IAuthorizationRequest AuthorizationRequest { get; } = authorizationRequest;

    /// <inheritdoc />
    public override bool IsContinuation { get; } = isContinuation;
}

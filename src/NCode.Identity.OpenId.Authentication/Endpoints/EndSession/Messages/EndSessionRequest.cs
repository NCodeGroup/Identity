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

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IEndSessionRequest"/> abstraction.
/// </summary>
public sealed class EndSessionRequest : OpenIdMessage<EndSessionRequest>, IEndSessionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EndSessionRequest"/> class.
    /// </summary>
    public EndSessionRequest()
    {
        // nothing
    }

    private EndSessionRequest(EndSessionRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? IdTokenHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdTokenHint);
        set => SetKnownParameter(OpenIdCommonParameters.IdTokenHint, value);
    }

    /// <inheritdoc />
    public string? ClientId
    {
        get => GetKnownParameter(OpenIdCommonParameters.ClientId);
        set => SetKnownParameter(OpenIdCommonParameters.ClientId, value);
    }

    /// <inheritdoc />
    public string? PostLogoutRedirectUri
    {
        get => GetKnownParameter(OpenIdCommonParameters.PostLogoutRedirectUri);
        set => SetKnownParameter(OpenIdCommonParameters.PostLogoutRedirectUri, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(OpenIdCommonParameters.State);
        set => SetKnownParameter(OpenIdCommonParameters.State, value);
    }

    /// <inheritdoc />
    public override EndSessionRequest Clone() => new(this);
}

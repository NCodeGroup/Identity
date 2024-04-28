﻿#region Copyright Preamble

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

using NCode.Identity.OpenId.Clients;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="AuthorizationRequestContext"/> abstraction.
/// </summary>
public class DefaultAuthorizationRequestContext(
    OpenIdContext openIdContext,
    OpenIdClient openIdClient,
    IAuthorizationRequest authorizationRequest,
    bool isContinuation
) : AuthorizationRequestContext
{
    /// <inheritdoc />
    public override OpenIdContext OpenIdContext { get; } = openIdContext;

    /// <inheritdoc />
    public override OpenIdClient OpenIdClient { get; } = openIdClient;

    /// <inheritdoc />
    public override IAuthorizationRequest AuthorizationRequest { get; } = authorizationRequest;

    /// <inheritdoc />
    public override bool IsContinuation { get; } = isContinuation;
}
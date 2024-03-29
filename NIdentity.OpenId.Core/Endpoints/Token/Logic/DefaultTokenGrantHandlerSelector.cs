﻿#region Copyright Preamble

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

using System.Diagnostics;
using NIdentity.OpenId.Endpoints.Token.Messages;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Token.Logic;

internal class DefaultTokenGrantHandlerSelector(
    IOpenIdErrorFactory errorFactory,
    IEnumerable<ITokenGrantHandler> handlers
) : ITokenGrantHandlerSelector
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    private Dictionary<string, ITokenGrantHandler> Handlers { get; } =
        handlers.ToDictionary(handler => handler.GrantType, StringComparer.Ordinal);

    public ValueTask<ITokenGrantHandler> SelectAsync(
        TokenRequestContext tokenRequestContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var grantType = tokenRequestContext.TokenRequest.GrantType;
        Debug.Assert(grantType is not null);

        if (!Handlers.TryGetValue(grantType, out var handler))
        {
            throw ErrorFactory
                .UnsupportedGrantType("The specified grant type is not supported.")
                .AsException();
        }

        return ValueTask.FromResult(handler);
    }
}

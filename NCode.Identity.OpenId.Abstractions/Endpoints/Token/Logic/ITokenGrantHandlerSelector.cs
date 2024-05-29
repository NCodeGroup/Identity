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

using NCode.Identity.OpenId.Endpoints.Token.Contexts;

namespace NCode.Identity.OpenId.Endpoints.Token.Logic;

// TODO: delete

/// <summary>
/// Provides the ability to select the <see cref="ITokenGrantHandler"/> instance that the authorization server will use to
/// issue tokens based on the specified <see cref="TokenRequestContext"/>.
/// </summary>
public interface ITokenGrantHandlerSelector
{
    /// <summary>
    /// Gets the <see cref="ITokenGrantHandler"/> instance that the authorization server will use to
    /// issue tokens based on the specified <paramref name="tokenRequestContext"/>.
    /// </summary>
    /// <param name="tokenRequestContext">The <see cref="TokenRequestContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ITokenGrantHandler"/>
    /// instance that will be used to issue tokens based on the specified <paramref name="tokenRequestContext"/>.</returns>
    ValueTask<ITokenGrantHandler> SelectAsync(
        TokenRequestContext tokenRequestContext,
        CancellationToken cancellationToken);
}

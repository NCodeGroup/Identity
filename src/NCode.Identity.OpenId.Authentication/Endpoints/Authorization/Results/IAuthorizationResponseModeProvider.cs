#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Resolves a <c>response_mode</c> to the registered <see cref="IAuthorizationResponseModeHandler"/> that delivers it
/// (ADR-0201).
/// </summary>
internal interface IAuthorizationResponseModeProvider
{
    /// <summary>
    /// Resolves the requested response mode, or the response type's default when none is requested, to its registered
    /// handler. <c>jwt</c> resolves to the handler whose <see cref="IAuthorizationResponseModeHandler.SecuredResponseMode"/>
    /// is that default (JARM §2.3.4). Returns <see langword="false"/> when no registered handler serves it (ADR-0201).
    /// </summary>
    /// <param name="requestedResponseMode">The requested <c>response_mode</c>, or <see langword="null"/> when none is requested.</param>
    /// <param name="responseTypes">The names of the response type, in any order.</param>
    /// <param name="handler">The handler that delivers the response mode, when one is registered.</param>
    /// <returns><see langword="true"/> when a registered handler serves the response mode.</returns>
    bool TryResolve(
        string? requestedResponseMode,
        IReadOnlyCollection<string> responseTypes,
        [NotNullWhen(true)] out IAuthorizationResponseModeHandler? handler
    );
}

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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Provides the default implementation of <see cref="IAuthorizationResponseModeProvider"/> backed by the
/// <see cref="IAuthorizationResponseModeHandler"/> implementations registered in dependency injection (ADR-0201).
/// </summary>
/// <remarks>
/// The constructor throws an <see cref="InvalidOperationException"/> when the registered handlers are inconsistent: a
/// handler is named <c>jwt</c>, two handlers deliver the same response mode, two handlers secure the same response mode,
/// or a handler secures a response mode that no registered handler delivers.
/// </remarks>
internal class DefaultAuthorizationResponseModeProvider : IAuthorizationResponseModeProvider
{
    private FrozenDictionary<string, IAuthorizationResponseModeHandler> Handlers { get; }

    private FrozenDictionary<
        string,
        IAuthorizationResponseModeHandler
    > HandlersBySecuredResponseMode { get; }

    public DefaultAuthorizationResponseModeProvider(
        IEnumerable<IAuthorizationResponseModeHandler> handlers
    )
    {
        var handlersByResponseMode = new Dictionary<string, IAuthorizationResponseModeHandler>(
            StringComparer.Ordinal
        );
        var handlersBySecuredResponseMode = new Dictionary<
            string,
            IAuthorizationResponseModeHandler
        >(StringComparer.Ordinal);

        var handlerList = handlers.ToList();

        foreach (var handler in handlerList)
        {
            var responseMode = handler.ResponseMode;

            if (
                string.Equals(
                    responseMode,
                    OpenIdProtocol.ResponseModes.Jwt,
                    StringComparison.Ordinal
                )
            )
                throw new InvalidOperationException(
                    $"The '{handler.GetType().Name}' handler cannot deliver the '{responseMode}' response mode, "
                        + "because it is the JARM shorthand that resolves to a handler that secures the default response mode."
                );

            if (!handlersByResponseMode.TryAdd(responseMode, handler))
                throw new InvalidOperationException(
                    $"More than one handler delivers the '{responseMode}' response mode."
                );
        }

        foreach (var handler in handlerList)
        {
            var securedResponseMode = handler.SecuredResponseMode;
            if (securedResponseMode is null)
                continue;

            if (!handlersBySecuredResponseMode.TryAdd(securedResponseMode, handler))
                throw new InvalidOperationException(
                    $"More than one handler secures the '{securedResponseMode}' response mode."
                );

            if (!handlersByResponseMode.ContainsKey(securedResponseMode))
                throw new InvalidOperationException(
                    $"The '{handler.ResponseMode}' handler secures the '{securedResponseMode}' response mode, "
                        + "but no handler delivers it."
                );
        }

        Handlers = handlersByResponseMode.ToFrozenDictionary(StringComparer.Ordinal);
        HandlersBySecuredResponseMode = handlersBySecuredResponseMode.ToFrozenDictionary(
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public bool TryResolve(
        string? requestedResponseMode,
        IReadOnlyCollection<string> responseTypes,
        [NotNullWhen(true)] out IAuthorizationResponseModeHandler? handler
    )
    {
        var defaultResponseMode = ResponseTypeValues.GetDefaultResponseMode(responseTypes);

        if (requestedResponseMode is null)
            return Handlers.TryGetValue(defaultResponseMode, out handler);

        if (
            string.Equals(
                requestedResponseMode,
                OpenIdProtocol.ResponseModes.Jwt,
                StringComparison.Ordinal
            )
        )
            return HandlersBySecuredResponseMode.TryGetValue(defaultResponseMode, out handler);

        return Handlers.TryGetValue(requestedResponseMode, out handler);
    }
}

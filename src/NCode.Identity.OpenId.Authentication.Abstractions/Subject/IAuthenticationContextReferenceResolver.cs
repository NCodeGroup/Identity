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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides the ability to resolve the <c>acr</c> (Authentication Context Class Reference) value the authorization
/// server asserts for an authentication event. OpenID Connect defers the meaning of <c>acr</c> values to the
/// deployment, so this is a replaceable extensibility seam: register a custom implementation to map authentication
/// methods (and identity provider, settings, etc.) to the deployment's own authentication-context policy.
/// </summary>
[PublicAPI]
public interface IAuthenticationContextReferenceResolver
{
    /// <summary>
    /// Resolves the <c>acr</c> value for the described authentication event, or <see langword="null"/> when no
    /// authentication context class applies and the <c>acr</c> claim should be omitted.
    /// </summary>
    /// <param name="request">The inputs describing the authentication event.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
    /// <returns>The <c>acr</c> value to assert, or <see langword="null"/> to omit the claim.</returns>
    ValueTask<string?> ResolveAsync(
        AuthenticationContextReferenceRequest request,
        CancellationToken cancellationToken
    );
}

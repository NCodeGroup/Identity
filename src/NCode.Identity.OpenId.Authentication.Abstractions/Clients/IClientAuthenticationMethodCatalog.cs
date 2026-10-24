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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Resolves the registered client-authentication methods by name.
/// </summary>
/// <remarks>
/// The registered methods are the <see cref="ClientAuthenticationMethodDescriptor"/> instances that packages contribute.
/// The catalog is independent of the <see cref="IClientAuthenticationHandler"/> implementations, so reading it
/// constructs no handler.
/// </remarks>
// See ADR-0207.
[PublicAPI]
public interface IClientAuthenticationMethodCatalog
{
    /// <summary>
    /// Gets the names of the registered methods, in registration order.
    /// </summary>
    IReadOnlyList<string> MethodNames { get; }

    /// <summary>
    /// Attempts to get the descriptor of the registered method with the specified name.
    /// </summary>
    /// <param name="methodName">The name of the method, as a client registers it in its <c>token_endpoint_auth_method</c>.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the descriptor of the method; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a method with the specified name is registered; otherwise, <c>false</c>.</returns>
    bool TryGet(
        string methodName,
        [NotNullWhen(true)] out ClientAuthenticationMethodDescriptor? descriptor
    );
}

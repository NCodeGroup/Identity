// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
// See ADR-1145.
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

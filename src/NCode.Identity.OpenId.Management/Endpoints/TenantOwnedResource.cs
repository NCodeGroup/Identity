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
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Factory for <see cref="TenantOwnedResource{TValue}"/> that infers the wrapped value's type.
/// </summary>
internal static class TenantOwnedResource
{
    /// <summary>
    /// Creates a <see cref="TenantOwnedResource{TValue}"/> that pairs the specified resource with its owning tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the owning tenant.</param>
    /// <param name="value">The wrapped resource.</param>
    /// <typeparam name="TValue">The type of the wrapped resource.</typeparam>
    /// <returns>The tenant-scoped wrapper.</returns>
    public static TenantOwnedResource<TValue> For<TValue>(string tenantId, TValue value)
        where TValue : ISupportConcurrencyToken => new() { TenantId = tenantId, Value = value };

    /// <summary>
    /// Creates a <see cref="TenantOwnedResource{TValue}"/> for the specified resource, or returns <c>null</c> when
    /// the resource itself is <c>null</c> — convenient for wrapping the result of a load-or-default lookup.
    /// </summary>
    /// <param name="tenantId">The identifier of the owning tenant.</param>
    /// <param name="value">The wrapped resource, or <c>null</c>.</param>
    /// <typeparam name="TValue">The type of the wrapped resource.</typeparam>
    /// <returns>The tenant-scoped wrapper, or <c>null</c> when <paramref name="value"/> is <c>null</c>.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static TenantOwnedResource<TValue>? ForOrDefault<TValue>(string tenantId, TValue? value)
        where TValue : class, ISupportConcurrencyToken =>
        value is null ? null : For(tenantId, value);
}

/// <summary>
/// Wraps a persisted resource together with its owning tenant so that tenant-scoped authorization
/// (<c>TenantAdminHandler</c>, which only evaluates <see cref="ISupportTenantId"/> resources) applies to a
/// resource that does not itself carry a tenant identifier — most notably an individual secret loaded by id.
/// The <see cref="ConcurrencyToken"/> delegates to the wrapped value so the <c>ETag</c> flow is unaffected.
/// </summary>
/// <typeparam name="TValue">The type of the wrapped resource.</typeparam>
internal sealed class TenantOwnedResource<TValue> : ISupportTenantId, ISupportConcurrencyToken
    where TValue : ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the wrapped resource.
    /// </summary>
    public required TValue Value { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public string ConcurrencyToken => Value.ConcurrencyToken;
}

// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of <see cref="IAmbientTenantAccessor"/> that stores the ambient tenant scope in
/// an <see cref="AsyncLocal{T}"/> so it flows across asynchronous calls within a request, including data-access
/// components created outside the current dependency injection scope.
/// </summary>
internal sealed class DefaultAmbientTenantAccessor : IAmbientTenantAccessor
{
    private AsyncLocal<string?> AmbientTenantId { get; } = new();

    /// <inheritdoc />
    public bool IsScoped => AmbientTenantId.Value is not null;

    /// <inheritdoc />
    public string? TenantId => AmbientTenantId.Value;

    /// <inheritdoc />
    public IDisposable BeginScope(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        var previous = AmbientTenantId.Value;
        AmbientTenantId.Value = tenantId;
        return new Scope(this, previous);
    }

    private sealed class Scope(DefaultAmbientTenantAccessor owner, string? previous) : IDisposable
    {
        private bool Disposed { get; set; }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;
            owner.AmbientTenantId.Value = previous;
        }
    }
}

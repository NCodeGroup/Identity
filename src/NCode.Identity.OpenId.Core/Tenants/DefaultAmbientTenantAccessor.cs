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

using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of <see cref="IAmbientTenantAccessor"/> that stores the ambient tenant scope in
/// an <see cref="AsyncLocal{T}"/> so it flows across asynchronous calls within a request, including data-access
/// components created outside the current dependency injection scope.
/// </summary>
internal sealed class DefaultAmbientTenantAccessor : IAmbientTenantAccessor
{
    private readonly AsyncLocal<string?> _tenantId = new();

    /// <inheritdoc />
    public bool IsScoped => _tenantId.Value is not null;

    /// <inheritdoc />
    public string? TenantId => _tenantId.Value;

    /// <inheritdoc />
    public IDisposable BeginScope(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        var previous = _tenantId.Value;
        _tenantId.Value = tenantId;
        return new Scope(this, previous);
    }

    private sealed class Scope(DefaultAmbientTenantAccessor owner, string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            owner._tenantId.Value = previous;
        }
    }
}

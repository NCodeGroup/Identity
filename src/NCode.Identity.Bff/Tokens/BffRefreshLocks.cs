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

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// A fixed set of striped locks that serialize token refreshes inside one replica.
/// </summary>
internal sealed class BffRefreshLocks : IDisposable
{
    private const int StripeCount = 64;

    private readonly SemaphoreSlim[] _locks = CreateLocks();

    private static SemaphoreSlim[] CreateLocks()
    {
        var locks = new SemaphoreSlim[StripeCount];
        for (var i = 0; i < locks.Length; i++)
        {
            locks[i] = new SemaphoreSlim(1, 1);
        }

        return locks;
    }

    /// <summary>
    /// Gets the lock that guards the specified session.
    /// </summary>
    /// <param name="storeKey">The store key of the session.</param>
    /// <returns>The striped semaphore for the session.</returns>
    public SemaphoreSlim For(string storeKey)
    {
        var hash = StringComparer.Ordinal.GetHashCode(storeKey);
        var index = (uint)hash % StripeCount;
        return _locks[index];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var semaphore in _locks)
        {
            semaphore.Dispose();
        }
    }
}

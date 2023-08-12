#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Runtime.CompilerServices;

namespace NCode.Cryptography.Keys.Internal;

public abstract class BaseDisposable : IDisposable
{
    protected bool IsDisposed { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(GetType().FullName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T GetOrThrowObjectDisposed<T>(T value) =>
        IsDisposed ? throw new ObjectDisposedException(GetType().FullName) : value;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden in a derived class, releases the unmanaged resources used by this instance,
    /// and optionally releases any managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c>
    /// to release only unmanaged resources.</param>
    protected abstract void Dispose(bool disposing);
}

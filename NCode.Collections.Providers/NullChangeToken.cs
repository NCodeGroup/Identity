﻿#region Copyright Preamble

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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Disposables;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides an implementation of <see cref="IChangeToken"/> that never generates any change notifications.
/// </summary>
[PublicAPI]
public sealed class NullChangeToken : INullChangeToken
{
    /// <summary>
    /// Gets a singleton instance of <see cref="NullChangeToken"/>.
    /// </summary>
    public static NullChangeToken Singleton { get; } = new();

    /// <inheritdoc />
    public bool HasChanged => false;

    /// <inheritdoc />
    public bool ActiveChangeCallbacks => false;

    /// <inheritdoc />
    public IDisposable RegisterChangeCallback(Action<object> callback, object? state) => Disposable.Empty;
}

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

using Microsoft.Extensions.Primitives;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides the composition root (i.e. top-level collection) of <see cref="SecretKey"/> instances by aggregating multiple
/// <see cref="ISecretKeyDataSource"/> instances and providing change notifications.
/// </summary>
public interface ISecretKeyProvider : IDisposable
{
    /// <summary>
    /// Gets a read-only collection of <see cref="SecretKey"/> instances.
    /// </summary>
    ISecretKeyCollection SecretKeys { get; }

    /// <summary>
    /// Gets a <see cref="IChangeToken"/> that provides notifications when changes occur.
    /// </summary>
    IChangeToken GetChangeToken();
}
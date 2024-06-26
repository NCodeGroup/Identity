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

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Common abstraction for all <c>JOSE</c> algorithms.
/// </summary>
[PublicAPI]
public abstract class Algorithm
{
    /// <summary>
    /// Gets an <see cref="AlgorithmType"/> value that describes the type of the current algorithm.
    /// </summary>
    public abstract AlgorithmType Type { get; }

    /// <summary>
    /// Gets a <see cref="string"/> value that uniquely identifies the current algorithm.
    /// </summary>
    public abstract string Code { get; }
}

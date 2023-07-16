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

namespace NCode.Jose;

/// <summary>
/// Common interface for all <c>JOSE</c> algorithms.
/// </summary>
public interface IAlgorithm
{
    /// <summary>
    /// Gets an <see cref="AlgorithmType"/> value that describes the type of the current algorithm.
    /// </summary>
    AlgorithmType Type { get; }

    /// <summary>
    /// Gets a <see cref="string"/> value that uniquely identifies the current algorithm.
    /// </summary>
    string Code { get; }
}

/// <summary>
/// Base implementation for all <c>JOSE</c> algorithms.
/// </summary>
public abstract class Algorithm : IAlgorithm
{
    /// <inheritdoc />
    public abstract AlgorithmType Type { get; }

    /// <inheritdoc />
    public abstract string Code { get; }
}

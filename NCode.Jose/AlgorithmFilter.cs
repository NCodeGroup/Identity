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

using Microsoft.Extensions.Options;

namespace NCode.Jose;

/// <summary>
/// Provides the ability to configure the list of support cryptographic algorithms.
/// </summary>
public interface IAlgorithmFilter
{
    /// <summary>
    /// Returns a boolean value whether the specified <paramref name="algorithm"/> should be excluded
    /// from the list of supported cryptographic algorithms.
    /// </summary>
    /// <param name="algorithm">The <see cref="IAlgorithm"/> to check if it should be excluded.</param>
    /// <returns><c>true</c> if the specified <paramref name="algorithm"/> should be excluded; otherwise, <c>false</c>.</returns>
    bool Exclude(IAlgorithm algorithm);
}

/// <summary>
/// Provides an implementation for the <see cref="IAlgorithmFilter"/> interface by excluding algorithms
/// that are configured in <see cref="JoseOptions"/>.
/// </summary>
public class AlgorithmFilter : IAlgorithmFilter
{
    private IReadOnlySet<string> DisabledAlgorithms { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmFilter"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An <see cref="IOptions{TOptions}"/> can that fetch <see cref="JoseOptions"/>.</param>
    public AlgorithmFilter(IOptions<JoseOptions> optionsAccessor)
    {
        DisabledAlgorithms = optionsAccessor.Value.DisabledAlgorithms.ToHashSet();
    }

    /// <inheritdoc />
    public bool Exclude(IAlgorithm algorithm) =>
        DisabledAlgorithms.Contains(algorithm.Code);
}

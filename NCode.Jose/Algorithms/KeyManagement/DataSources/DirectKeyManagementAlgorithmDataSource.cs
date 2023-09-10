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

using NCode.Jose.Algorithms.DataSources;

namespace NCode.Jose.Algorithms.KeyManagement.DataSources;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDataSource"/> that returns the core <c>JOSE</c> algorithm
/// with support for <c>Direct Encryption with a Shared Symmetric Key</c>.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.5
/// </summary>
public class DirectKeyManagementAlgorithmDataSource : StaticAlgorithmDataSource
{
    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // Direct Encryption with a Shared Symmetric Key
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.5

            yield return new DirectKeyManagementAlgorithm();
        }
    }
}

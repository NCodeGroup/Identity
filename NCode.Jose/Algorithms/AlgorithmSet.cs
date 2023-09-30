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

namespace NCode.Jose.Algorithms;

/// <summary>
/// Contains a set of string codes for various cryptographic algorithms.
/// </summary>
public class AlgorithmSet
{
    /// <summary>
    /// Gets or sets a collection of string codes representing signing algorithms.
    /// </summary>
    public IList<string> SigningAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a collection of string codes representing key management algorithms.
    /// </summary>
    public IList<string> KeyManagementAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a collection of string codes representing encryption algorithms.
    /// </summary>
    public IList<string> EncryptionAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a collection of string codes representing compression algorithms.
    /// </summary>
    public IList<string> CompressionAlgorithms { get; set; } = new List<string>();
}

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

namespace NCode.Identity.Jose.Algorithms;

public partial class AlgorithmCodes
{
    /// <summary>
    /// Contains constants of various supported algorithms used for compression.
    /// </summary>
    /// <remarks>
    /// References: https://datatracker.ietf.org/doc/html/rfc7516#section-4.1.3
    /// </remarks>
    public static class Compression
    {
        /// <summary>
        /// Specifies that compression is not used.
        /// </summary>
        public const string None = "";

        /// <summary>
        /// Compression with the DEFLATE (RFC1951) algorithm.
        /// </summary>
        public const string Deflate = "DEF";
    }
}

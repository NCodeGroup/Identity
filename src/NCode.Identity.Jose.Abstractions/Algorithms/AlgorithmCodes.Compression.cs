// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

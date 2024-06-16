#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text;
using JetBrains.Annotations;

namespace NCode.CryptoMemory;

/// <summary>
/// Provides secure encodings that throw an exception when invalid bytes are encountered.
/// </summary>
[PublicAPI]
public static class SecureEncoding
{
    private static ASCIIEncoding CreateSecureAsciiEncoding()
    {
        var encoding = (ASCIIEncoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        return encoding;
    }

    /// <summary>
    /// Gets an ASCII encoding that throws an exception when invalid bytes are encountered.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static ASCIIEncoding ASCII { get; } = CreateSecureAsciiEncoding();

    /// <summary>
    /// Gets a UTF-8 encoding that throws an exception when invalid bytes are encountered.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static UTF8Encoding UTF8 { get; } = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}

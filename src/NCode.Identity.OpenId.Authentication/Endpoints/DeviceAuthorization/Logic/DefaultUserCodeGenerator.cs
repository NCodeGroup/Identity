#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Security.Cryptography;
using System.Text;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="IUserCodeGenerator"/> abstraction that draws from a
/// transcription-resistant base-20 alphabet (no vowels, to avoid forming words, and no digits, to avoid glyphs the user
/// might confuse) and formats the code into dash-separated groups for display.
/// </summary>
internal sealed class DefaultUserCodeGenerator : IUserCodeGenerator
{
    // RFC 8628 §6.1 recommends a code set that minimizes transcription errors.
    private const string Alphabet = "BCDFGHJKLMNPQRSTVWXZ";
    private const int GroupSize = 4;

    /// <inheritdoc />
    public string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var builder = new StringBuilder(length + length / GroupSize);
        for (var index = 0; index < length; index++)
        {
            if (index > 0 && index % GroupSize == 0)
                builder.Append('-');

            builder.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public string Normalize(string userCode)
    {
        ArgumentNullException.ThrowIfNull(userCode);

        var builder = new StringBuilder(userCode.Length);
        foreach (var character in userCode)
        {
            var upper = char.ToUpperInvariant(character);
            if (Alphabet.Contains(upper))
                builder.Append(upper);
        }

        return builder.ToString();
    }
}

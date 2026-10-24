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

using System.Formats.Asn1;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Reads the RFC 5280 <c>ReasonFlags</c> as a mask: bit <c>i</c> is named bit <c>i</c>, from <c>keyCompromise</c> (1)
/// through <c>aACompromise</c> (8), and bit 0 is unused. The list of reasons is fixed by RFC 5280, so it is closed.
/// </summary>
internal static class CrlReasons
{
    /// <summary>
    /// The mask of every reason.
    /// </summary>
    public const int All = 0x1FE;

    /// <summary>
    /// Reads the reason flags that the specified tag holds and returns them as a mask.
    /// </summary>
    public static int Read(AsnReader reader, Asn1Tag tag)
    {
        var bits = reader.ReadNamedBitList(tag);
        var mask = 0;
        for (var bit = 1; bit <= 8 && bit < bits.Length; bit++)
        {
            if (bits[bit])
                mask |= 1 << bit;
        }

        return mask;
    }
}

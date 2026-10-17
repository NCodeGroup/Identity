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

using System.Buffers;
using System.Diagnostics;
using System.Text;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Jose;

partial class JoseSerializer
{
    private void DecodeJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter
    )
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */
        Debug.Assert(compactJwt.ProtectionType == JoseProtectionTypes.Jws);

        var segments = compactJwt.Segments;
        if (segments.Count != JwsSegmentCount)
            throw new InvalidOperationException("Malformed JWS token.");

        var header = compactJwt.Header;
        var encodedHeader = segments[0].Span;
        var encodedPayload = segments[1].Span;
        var encodedSignature = segments[2].Span;

        VerifyJws(secretKey, header, encodedHeader, encodedPayload, encodedSignature);

        if (!header.TryGetPropertyValue<bool>(JoseClaimNames.Header.B64, out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            Base64Url.Decode(encodedPayload, payloadWriter);
        }
        else
        {
            SecureEncoding.UTF8.GetBytes(encodedPayload, payloadWriter);
        }
    }
}

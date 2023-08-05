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
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.Extensions;
using Nerdbank.Streams;

namespace NCode.Jose;

partial class JoseSerializer
{
    private string DecodeJws(CompactJwt compactJwt, SecretKey secretKey)
    {
        using var payloadBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        DecodeJws(compactJwt, secretKey, payloadBuffer);
        return DecodeUtf8(payloadBuffer);
    }

    private T? DeserializeJws<T>(CompactJwt compactJwt, SecretKey secretKey)
    {
        using var payloadBuffer = new Sequence<byte>(ArrayPool<byte>.Shared);
        DecodeJws(compactJwt, secretKey, payloadBuffer);
        return Deserialize<T>(payloadBuffer);
    }

    private void DecodeJws(
        CompactJwt compactJwt,
        SecretKey secretKey,
        IBufferWriter<byte> payloadWriter)
    {
        /*
              BASE64URL(UTF8(JWS Protected Header)) || '.' ||
              BASE64URL(JWS Payload) || '.' ||
              BASE64URL(JWS Signature)
        */
        Debug.Assert(compactJwt.ProtectionType == JoseConstants.JWS);

        // JWS Protected Header
        var jwsProtectedHeader = compactJwt.Segments.First;
        var encodedHeader = compactJwt.EncodedHeader;
        var header = compactJwt.DeserializedHeader;

        // JWS Payload
        var jwsPayload = jwsProtectedHeader.Next!;
        var encodedPayload = jwsPayload.Memory.Span;

        // JWS Signature
        var jwsSignature = jwsPayload.Next!;
        var encodedSignature = jwsSignature.Memory.Span;

        VerifyJws(secretKey, header, encodedHeader, encodedPayload, encodedSignature);

        if (!header.TryGetValue<bool>("b64", out var b64))
        {
            b64 = true;
        }

        if (b64)
        {
            Base64Url.Decode(encodedPayload, payloadWriter);
        }
        else
        {
            Encoding.UTF8.GetBytes(encodedPayload, payloadWriter);
        }
    }
}

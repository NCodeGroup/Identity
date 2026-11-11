// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Text;
using NCode.Buffers;
using NCode.Identity.Json;
using NCode.Identity.Secrets.Keys;

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
            DecodeBase64Url(encodedPayload, payloadWriter);
        }
        else
        {
            SecureEncoding.UTF8.GetBytes(encodedPayload, payloadWriter);
        }
    }
}

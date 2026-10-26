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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Writes a text body as strict UTF-8 bytes without adding a charset parameter.
/// </summary>
internal static class Utf8TextResults
{
    public static Utf8ContentHttpResult Create(string content, string contentType)
    {
        using var lease = BufferFactory.Rent(
            SecureEncoding.UTF8.GetByteCount(content),
            isSensitive: false,
            out Span<byte> utf8
        );
        SecureEncoding.UTF8.GetBytes(content, utf8);
        return TypedResults.Text(utf8, contentType); // copies the bytes
    }
}

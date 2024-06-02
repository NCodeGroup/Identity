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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace NCode.Identity.OpenId.Tokens.Models;

public readonly record struct CreateSecurityTokenRequest
{
    public required DateTimeOffset CreatedWhen { get; init; }

    public string? Nonce { get; init; }

    public string? State { get; init; }

    public IReadOnlyCollection<string>? Scopes { get; init; }

    public string? AuthorizationCode { get; init; }

    public string? AccessToken { get; init; }

    public ClaimsIdentity? Subject { get; init; }

    public string? SubjectId { get; init; }

    public AuthenticationProperties? AuthenticationProperties { get; init; }

    public IReadOnlyDictionary<string, object>? ExtraPayloadClaims { get; init; }

    public IReadOnlyDictionary<string, object>? ExtraSignatureHeaderClaims { get; init; }

    public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
}

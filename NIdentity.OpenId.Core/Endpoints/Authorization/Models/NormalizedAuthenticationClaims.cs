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

namespace NIdentity.OpenId.Endpoints.Authorization.Models;

public class NormalizedAuthenticationClaims
{
    public const string Key = ".normalized-authentication-claims";

    public string Issuer { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public DateTimeOffset IssuedWhen { get; set; }

    public DateTimeOffset ExpiresWhen { get; set; }

    public DateTimeOffset AuthTime { get; set; }
}
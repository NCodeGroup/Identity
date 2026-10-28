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

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for the <c>client_assertion_type</c> values used with JWT client-authentication assertions.
    /// </summary>
    public static class ClientAssertionTypes
    {
        /// <summary>
        /// The <c>client_assertion_type</c> value that identifies a JWT bearer client-authentication assertion,
        /// as defined by <see href="https://datatracker.ietf.org/doc/html/rfc7523#section-2.2">RFC 7523 §2.2</see>
        /// and OpenID Connect Core §9 (<c>private_key_jwt</c> and <c>client_secret_jwt</c>).
        /// </summary>
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    }
}

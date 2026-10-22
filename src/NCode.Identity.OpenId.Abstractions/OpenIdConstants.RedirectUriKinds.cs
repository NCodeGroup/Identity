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

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the names of the built-in redirect URI kinds that the <c>redirect_uri_kinds_supported</c> setting lists.
    /// The vocabulary is open: a package adds a kind by registering an <c>IRedirectUriKind</c>.
    /// </summary>
    public static class RedirectUriKinds
    {
        /// <summary>
        /// Contains the name of the redirect URI kind for an <c>https</c> URI.
        /// </summary>
        public const string Https = "https";

        /// <summary>
        /// Contains the name of the redirect URI kind for an <c>http</c> URI whose host is the loopback IP literal
        /// <c>127.0.0.1</c> or <c>[::1]</c> (RFC 8252 §7.3).
        /// </summary>
        public const string Loopback = "loopback";

        /// <summary>
        /// Contains the name of the redirect URI kind for any other <c>http</c> URI, <c>localhost</c> included.
        /// </summary>
        public const string Http = "http";

        /// <summary>
        /// Contains the name of the redirect URI kind for a URI whose scheme is a reverse domain name other than
        /// <c>http</c> and <c>https</c>, such as <c>com.example.app</c> (RFC 8252 §7.1).
        /// </summary>
        public const string PrivateUse = "private_use";
    }
}

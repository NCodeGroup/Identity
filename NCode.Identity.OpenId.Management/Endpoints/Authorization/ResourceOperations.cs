#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Endpoints.Authorization;

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class ResourceOperations
{
    [PublicAPI]
    [ExcludeFromCodeCoverage]
    public static class Servers
    {
        private const string Prefix = "servers";

        [PublicAPI]
        [ExcludeFromCodeCoverage]
        public static class Basic
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private const string Prefix = Servers.Prefix + "/basic";

            public const string Read = Prefix + "/read";
        }

        [PublicAPI]
        [ExcludeFromCodeCoverage]
        public static class Settings
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private const string Prefix = Servers.Prefix + "/settings";

            public const string Read = Prefix + "/read";
            public const string Update = Prefix + "/update";
        }

        [PublicAPI]
        [ExcludeFromCodeCoverage]
        public static class Secrets
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private const string Prefix = Servers.Prefix + "/secrets";

            public const string Read = Prefix + "/read";
            public const string Update = Prefix + "/update";
        }
    }
}

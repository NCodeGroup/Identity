#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using NIdentity.OpenId.Messages.Authorization;

namespace NIdentity.OpenId.Messages.Parsers
{
    internal static class ParameterParsers
    {
        public static readonly ResponseTypeParser ResponseType = new();
        public static readonly ResponseModeParser ResponseMode = new();
        public static readonly DisplayTypeParser DisplayType = new();
        public static readonly PromptTypeParser PromptType = new();
        public static readonly CodeChallengeMethodParser CodeChallengeMethod = new();
        public static readonly JsonParser<IRequestClaims> RequestClaims = new();

        public static readonly StringParser String = new();
        public static readonly StringSetParser StringSet = new();
        public static readonly TimeSpanParser TimeSpan = new();
        public static readonly UriParser Uri = new();
    }
}

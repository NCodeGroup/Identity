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

namespace NIdentity.OpenId.Messages.Parsers
{
    internal static class ParameterParsers
    {
        public static ResponseTypeParser ResponseType = new();
        public static ResponseModeParser ResponseMode = new();
        public static DisplayTypeParser DisplayType = new();
        public static PromptTypeParser PromptType = new();
        public static CodeChallengeMethodParser CodeChallengeMethod = new();

        public static StringParser String = new();
        public static StringSetParser StringSet = new();
        public static TimeSpanParser TimeSpan = new();
    }
}

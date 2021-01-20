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

using System;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages
{
    internal abstract class KnownParameter
    {
        protected KnownParameter(string name, OpenIdParameterLoader loader)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            Name = name;
            Loader = loader;
        }

        public string Name { get; }

        public OpenIdParameterLoader Loader { get; }
    }

    internal sealed class KnownParameter<T> : KnownParameter
    {
        public KnownParameter(string name, OpenIdParameterParser<T> parser)
            : base(name, parser)
        {
            Parser = parser;
        }

        public OpenIdParameterParser<T> Parser { get; }
    }
}

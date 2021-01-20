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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal static class KnownParameters
    {
        private static readonly IDictionary<string, KnownParameter> Registry = LoadRegistry();

        private static IDictionary<string, KnownParameter> LoadRegistry()
        {
            return new Dictionary<string, KnownParameter>(StringComparer.Ordinal)
            {
                [Scope.Name] = Scope,
                [ResponseType.Name] = ResponseType,
                [ResponseMode.Name] = ResponseMode,
                [ClientId.Name] = ClientId,
                [RedirectUri.Name] = RedirectUri,
                [State.Name] = State,
                [Nonce.Name] = Nonce,
                [Display.Name] = Display,
                [Prompt.Name] = Prompt,
                [MaxAge.Name] = MaxAge,
            };
        }

        public static readonly KnownParameter<IEnumerable<StringSegment>> Scope = new(OpenIdConstants.Parameters.Scope, ParameterParsers.StringSet);
        public static readonly KnownParameter<ResponseTypes> ResponseType = new(OpenIdConstants.Parameters.ResponseType, ParameterParsers.ResponseType);
        public static readonly KnownParameter<ResponseMode?> ResponseMode = new(OpenIdConstants.Parameters.ResponseMode, ParameterParsers.ResponseMode);
        public static readonly KnownParameter<StringSegment> ClientId = new(OpenIdConstants.Parameters.ClientId, ParameterParsers.SingleString);
        public static readonly KnownParameter<StringSegment> RedirectUri = new(OpenIdConstants.Parameters.RedirectUri, ParameterParsers.SingleString);
        public static readonly KnownParameter<StringSegment> State = new(OpenIdConstants.Parameters.State, ParameterParsers.SingleOrDefaultString);
        public static readonly KnownParameter<StringSegment> Nonce = new(OpenIdConstants.Parameters.Nonce, ParameterParsers.SingleOrDefaultString);
        public static readonly KnownParameter<DisplayType?> Display = new(OpenIdConstants.Parameters.Display, ParameterParsers.DisplayType);
        public static readonly KnownParameter<PromptTypes?> Prompt = new(OpenIdConstants.Parameters.Prompt, ParameterParsers.PromptType);
        public static readonly KnownParameter<TimeSpan?> MaxAge = new(OpenIdConstants.Parameters.MaxAge, ParameterParsers.TimeSpan);

        public static void Register(KnownParameter knownParameter)
        {
            if (Registry.ContainsKey(knownParameter.Name)) return;
            Registry[knownParameter.Name] = knownParameter;
        }

        public static bool TryGet(string parameterName, [NotNullWhen(true)] out KnownParameter? knownParameter)
        {
            return Registry.TryGetValue(parameterName, out knownParameter);
        }
    }
}

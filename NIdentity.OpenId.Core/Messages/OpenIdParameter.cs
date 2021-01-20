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

using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdParameter
    {
        public string ParameterName { get; }

        public KnownParameter? KnownParameter { get; }

        public OpenIdStringValues StringValues { get; private set; }

        public object? ParsedValue { get; private set; }

        public OpenIdParameter(KnownParameter knownParameter)
        {
            ParameterName = knownParameter.Name;
            KnownParameter = knownParameter;
        }

        public OpenIdParameter(string parameterName)
        {
            KnownParameters.TryGetKnownParameter(parameterName, out var knownParameter);

            ParameterName = parameterName;
            KnownParameter = knownParameter;
        }

        public virtual void SetParsedValue(object? parsedValue)
        {
            ParsedValue = parsedValue;
        }

        public virtual void Update(OpenIdStringValues stringValues, object? parsedValue)
        {
            StringValues = stringValues;
            ParsedValue = parsedValue;
        }

        public virtual bool TryLoad(OpenIdStringValues stringValues, out ValidationResult result)
        {
            var loader = KnownParameter?.Loader ?? OpenIdParameterLoader.Default;
            return loader.TryLoad(ParameterName, stringValues, this, out result);
        }
    }
}

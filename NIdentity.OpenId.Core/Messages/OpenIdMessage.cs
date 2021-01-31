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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal abstract class OpenIdMessage : IOpenIdMessage
    {
        internal IDictionary<string, Parameter> Parameters { get; } = new Dictionary<string, Parameter>(StringComparer.Ordinal);

        /// <inheritdoc />
        public IOpenIdMessageContext? Context { get; set; }

        /// <inheritdoc />
        public int Count => Parameters.Count;

        /// <inheritdoc />
        public IEnumerable<string> Keys => Parameters.Keys;

        /// <inheritdoc />
        public IEnumerable<StringValues> Values => Parameters.Values.Select(_ => _.StringValues);

        /// <inheritdoc />
        public StringValues this[string key] => Parameters[key].StringValues;

        /// <inheritdoc />
        public bool ContainsKey(string key) => Parameters.ContainsKey(key);

        /// <inheritdoc />
        public bool TryGetValue(string key, out StringValues value)
        {
            if (!Parameters.TryGetValue(key, out var parameter))
            {
                value = default;
                return false;
            }

            value = parameter.StringValues;
            return true;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => Parameters
            .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.StringValues))
            .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected T? GetKnownParameter<T>(KnownParameter<T> knownParameter)
        {
            var context = Context ?? throw new InvalidOperationException();

            var parameterName = knownParameter.Name;
            if (!Parameters.TryGetValue(parameterName, out var parameter))
                return default;

            if (parameter.ParsedValue is T parsedValue)
                return parsedValue;

            if (!knownParameter.Parser.TryParse(context, parameter.Descriptor, parameter.StringValues, out var result))
                return default;

            parameter.UpdateParsedValue(result.Value);
            return result.Value;
        }

        protected void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
        {
            var context = Context ?? throw new InvalidOperationException();

            var parameterName = knownParameter.Name;
            if (parsedValue is null)
            {
                Parameters.Remove(parameterName);
                return;
            }

            var stringValues = knownParameter.Parser.Serialize(context, parsedValue);
            if (StringValues.IsNullOrEmpty(stringValues))
            {
                Parameters.Remove(parameterName);
                return;
            }

            if (!Parameters.TryGetValue(parameterName, out var parameter))
            {
                parameter = new Parameter(new ParameterDescriptor(knownParameter));
                Parameters[parameterName] = parameter;
            }

            parameter.Load(stringValues, parsedValue);
        }

        /// <inheritdoc />
        public bool TryLoad(string parameterName, StringValues stringValues, out ValidationResult result)
        {
            var context = Context ?? throw new InvalidOperationException();

            if (!Parameters.TryGetValue(parameterName, out var parameter))
            {
                var descriptor = KnownParameters.TryGet(parameterName, out var knownParameter) ?
                    new ParameterDescriptor(knownParameter) :
                    new ParameterDescriptor(parameterName);

                parameter = new Parameter(descriptor);
                Parameters[parameterName] = parameter;
            }

            if (!parameter.TryLoad(context, stringValues, out result))
                return false;

            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}

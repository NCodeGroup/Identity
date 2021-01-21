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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal abstract class OpenIdMessage : IOpenIdMessage
    {
        private readonly ILogger _logger;
        private readonly IDictionary<string, ParameterStore> _parameters = new Dictionary<string, ParameterStore>(StringComparer.Ordinal);

        protected OpenIdMessage(ILogger logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public int Count => _parameters.Count;

        /// <inheritdoc />
        public IEnumerable<string> Keys => _parameters.Keys;

        /// <inheritdoc />
        public IEnumerable<StringValues> Values => _parameters.Values.Select(_ => _.StringValues);

        /// <inheritdoc />
        public StringValues this[string key] => _parameters[key].StringValues;

        /// <inheritdoc />
        public bool ContainsKey(string key) => _parameters.ContainsKey(key);

        /// <inheritdoc />
        public bool TryGetValue(string key, out StringValues value)
        {
            if (!_parameters.TryGetValue(key, out var parameter))
            {
                value = default;
                return false;
            }

            value = parameter.StringValues;
            return true;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => _parameters
            .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.StringValues))
            .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected T? GetKnownParameter<T>(KnownParameter<T> knownParameter)
        {
            var parameterName = knownParameter.Name;
            if (!_parameters.TryGetValue(parameterName, out var parameter))
                return default;

            if (parameter.ParsedValue is T parsedValue)
                return parsedValue;

            if (!knownParameter.Parser.TryParse(_logger, parameter.Descriptor, parameter.StringValues, out var result))
                return default;

            parameter.SetParsedValue(result.Value);
            return result.Value;
        }

        protected void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
        {
            var parameterName = knownParameter.Name;
            if (parsedValue is null)
            {
                _parameters.Remove(parameterName);
                return;
            }

            var stringValues = knownParameter.Parser.Serialize(parsedValue);
            if (StringValues.IsNullOrEmpty(stringValues))
            {
                _parameters.Remove(parameterName);
                return;
            }

            if (!_parameters.TryGetValue(parameterName, out var parameter))
            {
                parameter = new ParameterStore(ParameterDescriptor.Get(knownParameter));
                _parameters[parameterName] = parameter;
            }

            parameter.Update(stringValues, parsedValue);
        }

        /// <inheritdoc />
        public bool TryLoad(IEnumerable<KeyValuePair<string, StringValues>> parameters, out ValidationResult result)
        {
            parameters = parameters
                .GroupBy(
                    kvp => kvp.Key,
                    kvp => kvp.Value.AsEnumerable(),
                    StringComparer.Ordinal)
                .Select(
                    grouping => KeyValuePair.Create(
                        grouping.Key,
                        new StringValues(grouping.SelectMany(stringValues => stringValues).ToArray())));

            foreach (var (parameterName, stringValues) in parameters)
            {
                if (!_parameters.TryGetValue(parameterName, out var parameter))
                {
                    parameter = new ParameterStore(ParameterDescriptor.Get(parameterName));
                    _parameters[parameterName] = parameter;
                }

                if (!parameter.TryLoad(_logger, stringValues, out result))
                    return false;
            }

            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}

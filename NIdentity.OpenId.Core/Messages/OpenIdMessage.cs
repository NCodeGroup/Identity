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

namespace NIdentity.OpenId.Messages
{
    internal abstract class OpenIdMessage : IOpenIdMessage
    {
        private readonly IOpenIdMessageContext? _context;

        internal IDictionary<string, Parameter> Parameters { get; } = new Dictionary<string, Parameter>(StringComparer.Ordinal);

        /// <inheritdoc />
        public IOpenIdMessageContext Context
        {
            get => _context ?? throw new InvalidOperationException("TODO");
            init => _context = value;
        }

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

        protected internal T? GetKnownParameter<T>(KnownParameter<T> knownParameter) =>
            Parameters.TryGetValue(knownParameter.Name, out var parameter) &&
            parameter.ParsedValue is T parsedValue ?
                parsedValue :
                default;

        protected internal void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
        {
            var parameterName = knownParameter.Name;
            if (parsedValue is null)
            {
                Parameters.Remove(parameterName);
                return;
            }

            var stringValues = knownParameter.Parser.Serialize(Context, parsedValue);
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

            parameter.Update(stringValues, parsedValue);
        }

        /// <inheritdoc />
        public void LoadParameter(string parameterName, StringValues stringValues)
        {
            if (!Parameters.TryGetValue(parameterName, out var parameter))
            {
                var descriptor = KnownParameters.TryGet(parameterName, out var knownParameter) ?
                    new ParameterDescriptor(knownParameter) :
                    new ParameterDescriptor(parameterName);

                parameter = new Parameter(descriptor);
                Parameters[parameterName] = parameter;
            }

            parameter.Load(Context, stringValues);
        }
    }
}

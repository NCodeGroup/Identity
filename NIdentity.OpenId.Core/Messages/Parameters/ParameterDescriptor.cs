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

namespace NIdentity.OpenId.Messages.Parameters
{
    internal readonly struct ParameterDescriptor : IEquatable<ParameterDescriptor>
    {
        private static readonly ParameterLoader DefaultLoader = new();

        public ParameterDescriptor(KnownParameter knownParameter)
        {
            ParameterName = knownParameter.Name;
            KnownParameter = knownParameter;
        }

        public ParameterDescriptor(string parameterName)
        {
            ParameterName = parameterName;
            KnownParameter = null;
        }

        public string ParameterName { get; }

        public KnownParameter? KnownParameter { get; }

        public bool Optional => KnownParameter?.Optional ?? true;

        public bool AllowMultipleValues => KnownParameter?.AllowMultipleValues ?? true;

        public ParameterLoader Loader => KnownParameter?.Loader ?? DefaultLoader;

        // disallow boxing
        public override bool Equals(object? obj) =>
            throw new InvalidOperationException();

        public bool Equals(ParameterDescriptor other) =>
            KnownParameter == null ?
                string.Equals(ParameterName, other.ParameterName, StringComparison.Ordinal) :
                KnownParameter == other.KnownParameter;

        public override int GetHashCode() =>
            KnownParameter?.GetHashCode() ?? StringComparer.Ordinal.GetHashCode(ParameterName);
    }
}

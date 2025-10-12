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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Represents a strongly typed key in a <see cref="IParameterCollection"/>.
/// </summary>
/// <typeparam name="T">The type of the parameter's value.</typeparam>
[PublicAPI]
public readonly record struct ParameterKey<T>(string ParameterName)
{
    /// <summary>
    /// Gets the type of the parameter's value.
    /// </summary>
    public Type ValueType => typeof(T);
}

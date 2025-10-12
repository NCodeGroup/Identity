#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a default implementation of the <see cref="IKnownParameterCollection"/> abstraction.
/// </summary>
public class KnownParameterCollection(
    IEnumerable<KnownParameter> knownParameters
) : IKnownParameterCollection
{
    private Dictionary<string, KnownParameter> KnownParameters { get; } =
        knownParameters.ToDictionary(x => x.Name, StringComparer.Ordinal);

    /// <inheritdoc />
    public int Count =>
        KnownParameters.Count;

    /// <inheritdoc />
    public bool TryGet(string parameterName, [MaybeNullWhen(false)] out KnownParameter knownParameter) =>
        KnownParameters.TryGetValue(parameterName, out knownParameter);

    /// <inheritdoc />
    public IEnumerator<KnownParameter> GetEnumerator() =>
        KnownParameters.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}

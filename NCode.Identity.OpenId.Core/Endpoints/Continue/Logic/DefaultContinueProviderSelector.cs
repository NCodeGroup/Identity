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

namespace NCode.Identity.OpenId.Endpoints.Continue.Logic;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProviderSelector"/> abstraction.
/// </summary>
public class DefaultContinueProviderSelector(
    IEnumerable<IContinueProvider> providers
) : IContinueProviderSelector
{
    private Dictionary<string, IContinueProvider> Lookup { get; }
        = providers.ToDictionary(x => x.ContinueCode, StringComparer.Ordinal);

    /// <inheritdoc />
    public IContinueProvider SelectProvider(string continueCode)
    {
        if (!Lookup.TryGetValue(continueCode, out var provider))
        {
            throw new InvalidOperationException($"No continue provider found with continue code '{continueCode}'.");
        }

        return provider;
    }
}

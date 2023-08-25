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

using System.Text.Json;
using NCode.Jose.Json;

namespace NCode.Jose;

/// <summary>
/// Contains options for Jose services and algorithms.
/// </summary>
public class JoseOptions
{
    /// <summary>
    /// Gets or sets a list containing the codes of all the disabled algorithms.
    /// </summary>
    public List<string> DisabledAlgorithms { get; set; } = new();

    /// <summary>
    /// Gets or sets a boolean indicating whether to add the <c>kid</c> header during JOSE encoding.
    /// </summary>
    public bool AddKeyIdHeaderDuringEncode { get; set; } = true;

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> that is used for JSON serialization.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JoseObjectJsonConverter() }
    };
}

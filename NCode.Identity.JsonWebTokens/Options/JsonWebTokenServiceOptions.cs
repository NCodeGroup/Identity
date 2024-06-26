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

using JetBrains.Annotations;

namespace NCode.Identity.JsonWebTokens.Options;

/// <summary>
/// Contains configurable options for <see cref="DefaultJsonWebTokenService"/>.
/// </summary>
[PublicAPI]
public class JsonWebTokenServiceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether generated tokens will have default values for the 'iat', 'nbf', and 'exp' claims if not specified.
    /// </summary>
    public bool EnsureTokenLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets the default lifetime for generated tokens.
    /// </summary>
    public TimeSpan DefaultTokenLifetime { get; } = TimeSpan.FromMinutes(60);
}

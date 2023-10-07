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

namespace NCode.Identity.JsonWebTokens.Options;

/// <summary>
/// Provides the ability to configure the behavior when validating the lifetime of a Json Web Token (JWT).
/// </summary>
public class ValidateTokenLifeTimeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the <c>exp</c> claim is required.
    /// The default is <c>true</c>.
    /// </summary>
    public bool RequireExpirationTime { get; set; } = true;
}

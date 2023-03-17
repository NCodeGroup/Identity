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

namespace NIdentity.OpenId;

/// <summary>
/// Specifies which transformation to use when verifying the PKCE code challenge.
/// </summary>
public enum CodeChallengeMethod
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies to use the <c>plain</c> PKCE code challenge method.
    /// </summary>
    Plain,

    /// <summary>
    /// Specifies to use the <c>S256</c> PKCE code challenge method.
    /// </summary>
    Sha256
}

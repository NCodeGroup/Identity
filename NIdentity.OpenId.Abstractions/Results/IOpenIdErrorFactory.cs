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

namespace NIdentity.OpenId.Results;

/// <summary>
/// Provides the ability to create an <see cref="IOpenIdError"/> instance that represents an <c>OAuth</c> or <c>OpenID Connect</c> error.
/// </summary>
public interface IOpenIdErrorFactory
{
    /// <summary>
    /// Creates an <see cref="IOpenIdError"/> instance for an <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// </summary>
    /// <param name="errorCode">The <c>error</c> parameter for the <c>OAuth</c> or <c>OpenID Connect</c> error.</param>
    /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
    IOpenIdError Create(string errorCode);
}

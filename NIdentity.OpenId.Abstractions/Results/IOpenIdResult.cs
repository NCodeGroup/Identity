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

using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Defines a contract that represents the result of an <c>OAuth</c> or <c>OpenID Connect</c> operation.
/// </summary>
public interface IOpenIdResult
{
    /// <summary>
    /// Executes the result of the <c>OAuth</c> or <c>OpenID Connect</c> operation asynchronously.
    /// This method is called by the framework to process the result of an <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    /// <param name="context">The context in which the operation is executed. The context includes information about the
    /// operation that was executed and request information.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ExecuteResultAsync(OpenIdEndpointContext context);
}

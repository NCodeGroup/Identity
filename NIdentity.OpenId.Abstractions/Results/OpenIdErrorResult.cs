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
/// An implementation of <see cref="IOpenIdResult"/> that when executed, will render an <see cref="IOpenIdError"/>.
/// </summary>
public class OpenIdErrorResult : OpenIdResult<OpenIdErrorResult>
{
    /// <summary>
    /// Gets the <see cref="IOpenIdError"/>.
    /// </summary>
    public IOpenIdError Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdErrorResult"/> class.
    /// </summary>
    /// <param name="error"><see cref="IOpenIdError"/></param>
    public OpenIdErrorResult(IOpenIdError error)
    {
        Error = error;
    }
}

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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Contains the details for an <c>OAuth</c> or <c>OpenID Connect</c> error response.
/// </summary>
[PublicAPI]
public interface IOpenIdError : IOpenIdResponse, ISupportStatusCode
{
    /// <summary>
    /// Gets or sets the <see cref="Exception"/> that triggered the <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// May be <c>null</c> when protocol violations occur such as validations or other non-transient errors.
    /// </summary>
    Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the <c>error</c> parameter.
    /// </summary>
    string Code { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_description</c> parameter.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_uri</c> parameter.
    /// </summary>
    Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }
}

#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Provides various methods for interacting with OpenID subjects (aka users).
/// </summary>
public interface ISubjectService
{
    /// <summary>
    /// Validates whether the subject is active or not.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="openIdMessage">The <see cref="IOpenIdMessage"/> that represents the current OpenID request.</param>
    /// <param name="subjectAuthentication">The <see cref="SubjectAuthentication"/> that represents the subject to validate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the result of the validation.</returns>
    ValueTask<bool> ValidateIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdMessage openIdMessage,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken);
}

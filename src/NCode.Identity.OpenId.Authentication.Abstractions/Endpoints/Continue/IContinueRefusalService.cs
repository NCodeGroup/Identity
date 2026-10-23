#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides the ability to refuse a parked operation by its continue URL, so that the party that started the operation
/// is answered with an error instead of the operation being resumed.
/// </summary>
[PublicAPI]
public interface IContinueRefusalService
{
    /// <summary>
    /// Spends the continuation that a continue URL names and ends its operation with an error, so the party that
    /// started the operation is answered (an authorization request receives the error at its redirect URI). The result
    /// is not handled when the URL names no parked continuation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="continueUrl">The <see cref="string"/> continue URL that names the parked continuation.</param>
    /// <param name="error">The <see cref="IOpenIdError"/> that ends the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> for the operation.</returns>
    ValueTask<ReadOnlyEndpointDisposition> RefuseAsync(
        OpenIdContext openIdContext,
        string continueUrl,
        IOpenIdError error,
        CancellationToken cancellationToken
    );
}

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
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.PropertyBag;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// An opaque context that is used when validating a Json Web Token (JWT).
/// </summary>
/// <param name="secretKey">The <see cref="SecretKey"/> that was used to decode the Json Web Token (JWT).</param>
/// <param name="decodedJwt">A <see cref="DecodedJwt"/> that contains the decoded Json Web Token (JWT) header and payload.</param>
/// <param name="propertyBag">A <see cref="PropertyBag"/> that can be used to store custom state information.</param>
/// <param name="serviceProvider">An <see cref="IServiceProvider"/> that can be used to resolve services.</param>
/// <param name="timeProvider">A <see cref="TimeProvider"/> that can be used to get the current time.</param>
[PublicAPI]
public sealed class ValidateJwtContext(
    SecretKey secretKey,
    DecodedJwt decodedJwt,
    IPropertyBag propertyBag,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider
)
{
    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to decode the Json Web Token (JWT).
    /// </summary>
    public SecretKey SecretKey { get; } = secretKey;

    /// <summary>
    /// Gets a <see cref="DecodedJwt"/> that contains the decoded Json Web Token (JWT) header and payload.
    /// </summary>
    public DecodedJwt DecodedJwt { get; } = decodedJwt;

    /// <summary>
    /// Gets a <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.
    /// </summary>
    public IPropertyBag PropertyBag { get; } = propertyBag;

    /// <summary>
    /// Gets a <see cref="IServiceProvider"/> that can be used to resolve services.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets a <see cref="TimeProvider"/> that can be used to get the current time.
    /// </summary>
    public TimeProvider TimeProvider { get; } = timeProvider;
}

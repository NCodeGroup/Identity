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

using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens.Options;
using NCode.Identity.Secrets;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides a default implementation for the <see cref="IJsonWebTokenService"/> interface.
/// </summary>
public sealed partial class DefaultJsonWebTokenService : IJsonWebTokenService
{
    private JsonWebTokenServiceOptions Options { get; }
    private IServiceProvider ServiceProvider { get; }
    private TimeProvider TimeProvider { get; }
    private IJoseSerializer JoseSerializer { get; }
    private ISecretKeyProvider SecretKeyProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultJsonWebTokenService"/> class.
    /// </summary>
    /// <param name="optionsAccessor">An accessor that provides <see cref="JsonWebTokenServiceOptions"/>.</param>
    /// <param name="serviceProvider">An <see cref="IServiceProvider"/> that can be used to resolve services.</param>
    /// <param name="timeProvider">An <see cref="TimeProvider"/> that can be used to get the current time.</param>
    /// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
    /// <param name="secretKeyProvider">An <see cref="ISecretKeyProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
    public DefaultJsonWebTokenService(
        IOptions<JsonWebTokenServiceOptions> optionsAccessor,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        IJoseSerializer joseSerializer,
        ISecretKeyProvider secretKeyProvider)
    {
        Options = optionsAccessor.Value;
        ServiceProvider = serviceProvider;
        TimeProvider = timeProvider;
        JoseSerializer = joseSerializer;
        SecretKeyProvider = secretKeyProvider;
    }
}

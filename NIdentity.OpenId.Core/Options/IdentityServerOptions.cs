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

using Microsoft.AspNetCore.Authentication;

namespace NIdentity.OpenId.Options;

public class IdentityServerOptions
{
    public IdentityServerEvents Events { get; } = new();

    public string AuthenticationScheme { get; set; } = "TODO";

    /// <summary>
    /// Gets or sets the authentication scheme corresponding to the middleware
    /// responsible of persisting user's identity after a successful authentication.
    /// This value typically corresponds to a cookie middleware registered in the Startup class.
    /// When omitted, <see cref="AuthenticationOptions.DefaultSignInScheme"/> is used as a fallback value.
    /// </summary>
    public string? SignInScheme { get; set; }

    /// <summary>
    /// Gets or sets the amount of time to allow for clock skew when validating <see cref="DateTime"/> claims.
    /// The default is <c>300</c> seconds (5 minutes).
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5.0);
}

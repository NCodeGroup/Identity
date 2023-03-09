#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Contexts;

/// <summary>
/// Base class used by other context classes for authentication events.
/// </summary>
public abstract class BaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseContext"/> class.
    /// </summary>
    /// <param name="options"><see cref="IdentityServerOptions"/></param>
    /// <param name="httpContext"><see cref="HttpContext"/></param>
    protected BaseContext(IdentityServerOptions options, HttpContext httpContext)
    {
        Options = options;
        HttpContext = httpContext;
    }

    /// <summary>
    /// Gets the <see cref="IdentityServerOptions"/>.
    /// </summary>
    public virtual IdentityServerOptions Options { get; }

    /// <summary>
    /// Gets the <see cref="HttpContext"/>.
    /// </summary>
    public virtual HttpContext HttpContext { get; }

    public virtual CancellationToken CancellationToken => HttpContext.RequestAborted;
}

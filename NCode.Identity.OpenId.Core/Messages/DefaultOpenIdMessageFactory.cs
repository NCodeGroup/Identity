#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdMessageFactory"/> abstraction.
/// </summary>
/// <typeparam name="T">The type of the <see cref="OpenIdMessage"/> to create.</typeparam>
public class DefaultOpenIdMessageFactory<T>(
    OpenIdEnvironment openIdEnvironment
) : IOpenIdMessageFactory
    where T : OpenIdMessage, new()
{
    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    /// <inheritdoc />
    public string TypeDiscriminator => typeof(T).Name;

    /// <inheritdoc />
    public IOpenIdMessage Create(IEnumerable<IParameter> parameters)
    {
        var message = new T();
        message.Initialize(OpenIdEnvironment, parameters);
        return message;
    }
}

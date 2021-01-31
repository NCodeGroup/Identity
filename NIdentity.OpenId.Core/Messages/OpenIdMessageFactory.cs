#region Copyright Preamble

// 
//    Copyright @ 2021 NCode Group
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageFactory : IOpenIdMessageFactory
    {
        private readonly ConcurrentDictionary<Type, Func<IOpenIdMessage>> _registry;

        public OpenIdMessageFactory()
        {
            _registry = new ConcurrentDictionary<Type, Func<IOpenIdMessage>>
            {
                [typeof(IAuthorizationRequestMessage)] = () => new AuthorizationRequestMessage()
            };
        }

        public IOpenIdMessageFactory Register<TMessage>(Func<TMessage> factory)
            where TMessage : IOpenIdMessage
        {
            _registry.TryAdd(typeof(TMessage), () => factory());
            return this;
        }

        public TMessage Create<TMessage>()
            where TMessage : IOpenIdMessage
        {
            if (!_registry.TryGetValue(typeof(TMessage), out var factory))
                throw new InvalidOperationException();

            return (TMessage)factory();
        }

        public bool TryLoad<TMessage>(ILoadContext context, IEnumerable<KeyValuePair<string, StringValues>> parameters, out ValidationResult<TMessage> result)
            where TMessage : IOpenIdMessage
        {
            var message = Create<TMessage>();

            if (!message.TryLoad(context, parameters, out var loadResult))
            {
                result = loadResult.As<TMessage>();
                return false;
            }

            result = ValidationResult.Factory.Success(message);
            return true;
        }
    }
}

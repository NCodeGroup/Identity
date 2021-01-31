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
using NIdentity.OpenId.Messages.Authorization;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageFactory : IOpenIdMessageFactory
    {
        private readonly ConcurrentDictionary<Type, Func<IOpenIdMessageContext, IOpenIdMessage>> _registry;

        public OpenIdMessageFactory()
        {
            _registry = new ConcurrentDictionary<Type, Func<IOpenIdMessageContext, IOpenIdMessage>>
            {
                [typeof(IAuthorizationRequestMessage)] = context => new AuthorizationRequestMessage { Context = context }
            };
        }

        public IOpenIdMessageFactory Register<TMessage>(Func<IOpenIdMessageContext, TMessage> factory)
            where TMessage : IOpenIdMessage
        {
            _registry.TryAdd(typeof(TMessage), context => factory(context));
            return this;
        }

        public TMessage Create<TMessage>(IOpenIdMessageContext context)
            where TMessage : IOpenIdMessage
        {
            if (!_registry.TryGetValue(typeof(TMessage), out var factory))
                throw new InvalidOperationException();

            return (TMessage)factory(context);
        }
    }
}

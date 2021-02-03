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
using Microsoft.Extensions.DependencyInjection;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageFactory : IOpenIdMessageFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public OpenIdMessageFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public IOpenIdMessageContext CreateContext()
        {
            var factory = _serviceProvider.GetRequiredService<Func<IOpenIdMessageContext>>();
            return factory();
        }

        /// <inheritdoc />
        public TMessage Create<TMessage>(IOpenIdMessageContext context)
            where TMessage : IOpenIdMessage
        {
            var factory = _serviceProvider.GetRequiredService<Func<IOpenIdMessageContext, TMessage>>();
            return factory(context);
        }
    }
}

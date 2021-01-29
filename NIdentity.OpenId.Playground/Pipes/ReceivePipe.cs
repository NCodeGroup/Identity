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

using System.Threading.Tasks;
using GreenPipes;
using NIdentity.OpenId.Playground.Contexts;

namespace NIdentity.OpenId.Playground.Pipes
{
    internal interface IReceivePipe : IPipe<IReceiveContext>
    {
    }

    internal class ReceivePipe : IReceivePipe
    {
        private readonly IPipe<IReceiveContext> _receivePipe;
        private readonly IConsumePipe _consumePipe;

        public ReceivePipe(IPipe<IReceiveContext> receivePipe, IConsumePipe consumePipe)
        {
            _receivePipe = receivePipe;
            _consumePipe = consumePipe;
        }

        public Task Send(IReceiveContext context) => _receivePipe.Send(context);

        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("receivePipe");
            _receivePipe.Probe(scope);
        }
    }
}

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
using GreenPipes.Filters;
using GreenPipes.Pipes;

namespace NIdentity.OpenId.Playground
{
    internal class CompositePipeRouter : IPipeRouter
    {
        private readonly IDynamicRouter<PipeContext> _router;

        public CompositePipeRouter()
        {
            _router = new DynamicRouter<PipeContext>(new CompositePipeContextConverterFactory());
        }

        public CompositePipeRouter(IPipeContextConverterFactory<PipeContext> other)
        {
            _router = new DynamicRouter<PipeContext>(new CompositePipeContextConverterFactory(other));
        }

        public Task Send(PipeContext context)
            => _router.Send(context);

        public void Probe(ProbeContext context)
            => _router.Probe(context);

        public ConnectHandle ConnectPipe<T>(IPipe<T> pipe)
            where T : class, PipeContext
            => _router.ConnectPipe(pipe);

        public ConnectHandle ConnectObserver<T>(IFilterObserver<T> observer)
            where T : class, PipeContext
            => _router.ConnectObserver(observer);

        public ConnectHandle ConnectObserver(IFilterObserver observer)
            => _router.ConnectObserver(observer);
    }
}

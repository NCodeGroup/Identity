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

using System.Threading;
using System.Threading.Tasks;

namespace NIdentity.OpenId.Handlers
{
    /// <summary>
    /// Defines a handler that accepts an input argument and doesn't return anything.
    /// </summary>
    /// <typeparam name="TRequestOrContext">The type of the input argument.</typeparam>
    public interface IRequestHandler<in TRequestOrContext>
    {
        /// <summary>
        /// Handles a request given an input argument.
        /// </summary>
        /// <param name="requestOrContext">The input argument for the request handler.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>A <see cref="ValueTask"/> that can be used to monitor the asynchronous operation.</returns>
        ValueTask HandleAsync(TRequestOrContext requestOrContext, CancellationToken cancellationToken);
    }
}

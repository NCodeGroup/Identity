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

using System.Text.Json;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Results
{
    public interface IHttpResultFactory
    {
        IHttpResult Ok();

        IHttpResult Ok<T>(T value);

        IHttpResult StatusCode(int statusCode);

        IHttpResult Object<T>(T value);

        IHttpResult Object<T>(T value, JsonSerializerOptions serializerOptions);

        IHttpResult Object<T>(int statusCode, T value);

        IHttpResult Object<T>(int statusCode, T value, JsonSerializerOptions serializerOptions);

        IHttpResult NotFound();

        IHttpResult Unauthorized(string challenge, string realm);

        IHttpResult Unauthorized(string challenge, string realm, IErrorDetails errorDetails);

        IHttpResult BadRequest();

        IHttpResult BadRequest<T>(T value);
    }
}

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

namespace NIdentity.OpenId.Validation
{
    public static class OpenIdExceptionExtensions
    {
        public static OpenIdException WithStatusCode(this OpenIdException exception, int statusCode)
        {
            exception.ErrorDetails.StatusCode = statusCode;
            return exception;
        }

        public static OpenIdException WithErrorUri(this OpenIdException exception, string errorUri)
        {
            exception.ErrorDetails.Uri = errorUri;
            return exception;
        }

        public static OpenIdException WithErrorDescription(this OpenIdException exception, string errorDescription)
        {
            exception.ErrorDetails.Description = errorDescription;
            return exception;
        }

        public static OpenIdException WithExtensionData(this OpenIdException exception, string propertyName, object? propertyValue)
        {
            exception.ErrorDetails.ExtensionData[propertyName] = propertyValue;
            return exception;
        }
    }
}

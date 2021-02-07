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

namespace NIdentity.OpenId.Validation
{
    public static class OpenIdExceptionFactoryExtensions
    {
        public static OpenIdException InvalidRequest(this IOpenIdExceptionFactory factory, string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithErrorDescription(errorDescription);
        }

        public static OpenIdException InvalidRequestUri(this IOpenIdExceptionFactory factory, string errorDescription, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri)
        {
            return factory.Create(errorCode).WithErrorDescription(errorDescription);
        }

        public static OpenIdException InvalidRequestUri(this IOpenIdExceptionFactory factory, Exception innerException, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri)
        {
            return factory.Create(errorCode, innerException).WithErrorDescription("An error occurred while attempting to fetch request_uri.");
        }

        public static OpenIdException MissingParameter(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithErrorDescription($"The request is missing the {parameterName} parameter.");
        }

        public static OpenIdException InvalidParameterValue(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithErrorDescription($"The request includes an invalid value for the {parameterName} parameter.");
        }

        public static OpenIdException TooManyParameterValues(this IOpenIdExceptionFactory factory, string parameterName, string errorCode = OpenIdConstants.ErrorCodes.InvalidRequest)
        {
            return factory.Create(errorCode).WithErrorDescription($"The request includes the {parameterName} parameter more than once.");
        }

        public static OpenIdException RequestJwtNotSupported(this IOpenIdExceptionFactory factory, string errorCode = OpenIdConstants.ErrorCodes.RequestNotSupported)
        {
            return factory.Create(errorCode).WithErrorDescription("The 'request' parameter is not supported.");
        }

        public static OpenIdException RequestUriNotSupported(this IOpenIdExceptionFactory factory, string errorCode = OpenIdConstants.ErrorCodes.RequestUriNotSupported)
        {
            return factory.Create(errorCode).WithErrorDescription("The request_uri parameter is not supported.");
        }

        public static OpenIdException FailedToDecodeJwt(this IOpenIdExceptionFactory factory, string errorCode, Exception? innerException = null)
        {
            return factory.Create(errorCode, innerException).WithErrorDescription("An error occurred while attempting to decode the JWT value.");
        }

        public static OpenIdException FailedToDeserializeJson(this IOpenIdExceptionFactory factory, string errorCode, Exception? innerException = null)
        {
            return factory.Create(errorCode, innerException).WithErrorDescription("An error occurred while attempting to deserialize the JSON value.");
        }
    }
}

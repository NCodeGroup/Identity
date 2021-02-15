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
    /// <summary>
    /// Provides extension methods for <see cref="OpenIdException"/>.
    /// </summary>
    public static class OpenIdExceptionExtensions
    {
        /// <summary>
        /// Sets the <see cref="ErrorDetails.StatusCode"/> property on <see cref="OpenIdException.ErrorDetails"/>.
        /// </summary>
        /// <param name="exception">The <see cref="OpenIdException"/> to update.</param>
        /// <param name="statusCode">The value to set.</param>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException WithStatusCode(this OpenIdException exception, int statusCode)
        {
            exception.ErrorDetails.StatusCode = statusCode;
            return exception;
        }

        /// <summary>
        /// Sets the <see cref="ErrorDetails.Uri"/> property on <see cref="OpenIdException.ErrorDetails"/>.
        /// </summary>
        /// <param name="exception">The <see cref="OpenIdException"/> to update.</param>
        /// <param name="errorUri">The value to set.</param>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException WithErrorUri(this OpenIdException exception, string errorUri)
        {
            exception.ErrorDetails.Uri = errorUri;
            return exception;
        }

        /// <summary>
        /// Sets the <see cref="ErrorDetails.Description"/> property on <see cref="OpenIdException.ErrorDetails"/>.
        /// </summary>
        /// <param name="exception">The <see cref="OpenIdException"/> to update.</param>
        /// <param name="errorDescription">The value to set.</param>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException WithErrorDescription(this OpenIdException exception, string errorDescription)
        {
            exception.ErrorDetails.Description = errorDescription;
            return exception;
        }

        /// <summary>
        /// Adds an additional JSON property to be included when <see cref="IErrorDetails"/> is serialized.
        /// </summary>
        /// <param name="exception">The <see cref="OpenIdException"/> to update.</param>
        /// <param name="propertyName">The name of the JSON property.</param>
        /// <param name="propertyValue">The value of the JSON property.</param>
        /// <returns>The <see cref="OpenIdException"/> instance.</returns>
        public static OpenIdException WithExtensionData(this OpenIdException exception, string propertyName, object? propertyValue)
        {
            exception.ErrorDetails.ExtensionData[propertyName] = propertyValue;
            return exception;
        }
    }
}

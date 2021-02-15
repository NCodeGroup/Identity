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
using System.Runtime.Serialization;

namespace NIdentity.OpenId.Validation
{
    [Serializable]
    public class OpenIdException : Exception
    {
        public static IOpenIdExceptionFactory Factory { get; set; } = OpenIdExceptionFactory.Instance;

        public IErrorDetails ErrorDetails { get; }

        public OpenIdException(string message, string errorCode)
            : base(message)
        {
            ErrorDetails = new ErrorDetails(errorCode);
        }

        public OpenIdException(string message, string errorCode, Exception? inner)
            : base(message, inner)
        {
            ErrorDetails = new ErrorDetails(errorCode);
        }

        protected OpenIdException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorDetails = (ErrorDetails)(info.GetValue(nameof(ErrorDetails), typeof(ErrorDetails)) ?? new ErrorDetails(OpenIdConstants.ErrorCodes.ServerError));
        }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(ErrorDetails), ErrorDetails, typeof(ErrorDetails));
        }
    }
}

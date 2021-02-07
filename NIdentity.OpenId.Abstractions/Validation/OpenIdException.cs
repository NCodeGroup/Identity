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
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NIdentity.OpenId.Validation
{
    [Serializable]
    public class OpenIdException : Exception
    {
        public static IOpenIdExceptionFactory Factory { get; set; } = OpenIdExceptionFactory.Instance;

        public int? StatusCode { get; set; }

        public string ErrorCode { get; }

        public string? ErrorDescription { get; set; }

        public string? ErrorUri { get; set; }

        public Dictionary<string, object?> ExtensionData { get; set; } = new();

        public OpenIdException(string message, string errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public OpenIdException(string message, string errorCode, Exception? inner)
            : base(message, inner)
        {
            ErrorCode = errorCode;
        }

        protected OpenIdException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            StatusCode = (int?)info.GetValue(nameof(StatusCode), typeof(int?));
            ErrorCode = (string)info.GetValue(nameof(ErrorCode), typeof(string));
            ErrorDescription = (string?)info.GetValue(nameof(ErrorDescription), typeof(string));
            ErrorUri = (string?)info.GetValue(nameof(ErrorUri), typeof(string));
            ExtensionData = (Dictionary<string, object?>)info.GetValue(nameof(ExtensionData), typeof(Dictionary<string, object?>));
        }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(StatusCode), StatusCode, typeof(int?));
            info.AddValue(nameof(ErrorCode), ErrorCode, typeof(string));
            info.AddValue(nameof(ErrorDescription), ErrorDescription, typeof(string));
            info.AddValue(nameof(ErrorUri), ErrorUri, typeof(string));
            info.AddValue(nameof(ExtensionData), ExtensionData, typeof(Dictionary<string, object?>));
        }
    }
}

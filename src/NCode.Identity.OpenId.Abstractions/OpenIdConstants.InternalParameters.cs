#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the names of values the OP keeps in a message's property bag; the <c>$</c> prefix keeps them off the wire.
    /// </summary>
    public static class InternalParameters
    {
        /// <summary>
        /// Contains the name of <c>$authorization_source_type</c> parameter.
        /// </summary>
        public const string AuthorizationSourceType = "$authorization_source_type";

        /// <summary>
        /// Contains the name of <c>$created_when</c> parameter.
        /// </summary>
        public const string CreatedWhen = "$created_when";

        /// <summary>
        /// Contains the name of <c>$request_object_source</c> parameter.
        /// </summary>
        public const string RequestObjectSource = "$request_object_source";
    }
}

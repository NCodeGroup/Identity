// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

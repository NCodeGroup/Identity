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

namespace NIdentity.OpenId;

/// <summary>
/// Specifies the mechanism to be used for returning Authorization Response parameters from the Authorization
/// Endpoint.
/// </summary>
public enum ResponseMode
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies that Authorization Response parameters are encoded in the query string added to the
    /// <c>redirect_uri</c> when redirecting back to the Client.
    /// </summary>
    Query,

    /// <summary>
    /// Specifies that Authorization Response parameters are encoded in the fragment added to the
    /// <c>redirect_uri</c> when redirecting back to the Client.
    /// </summary>
    Fragment,

    /// <summary>
    /// Specifies that Authorization Response parameters are encoded as HTML form values that are auto-submitted in
    /// the User Agent, and thus are transmitted via the HTTP <c>POST</c> method to the Client, with the result
    /// parameters being encoded in the body using the <c>application/x-www-form-urlencoded</c> format.
    /// </summary>
    FormPost
}

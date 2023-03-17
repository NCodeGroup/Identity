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

namespace NIdentity.OpenId;

/// <summary>
/// Specifies how the Authorization Server displays the authentication and consent user interface pages to the
/// End-User. The Authorization Server MAY also attempt to detect the capabilities of the User Agent and present an
/// appropriate display.
/// </summary>
public enum DisplayType
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The Authorization Server SHOULD display the authentication and consent UI consistent with a full User Agent
    /// page view. If the display parameter is not specified, this is the default display mode.
    /// </summary>
    Page,

    /// <summary>
    /// The Authorization Server SHOULD display the authentication and consent UI consistent with a popup User Agent
    /// window. The popup User Agent window should be of an appropriate size for a login-focused dialog and should
    /// not obscure the entire window that it is popping up over.
    /// </summary>
    Popup,

    /// <summary>
    /// The Authorization Server SHOULD display the authentication and consent UI consistent with a device that
    /// leverages a touch interface.
    /// </summary>
    Touch,

    /// <summary>
    /// The Authorization Server SHOULD display the authentication and consent UI consistent with a "feature phone"
    /// type display.
    /// </summary>
    Wap
}

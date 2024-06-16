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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Mediator.Middleware;

/// <summary>
/// Contains the resulting state when handling an error in an exception handler that doesn't return a response.
/// </summary>
[PublicAPI]
public class CommandExceptionHandlerState
{
    /// <summary>
    /// Gets a value indicating whether the exception has been handled.
    /// </summary>
    public bool IsHandled { get; private set; }

    /// <summary>
    /// Sets the exception as handled so that it's not re-thrown.
    /// </summary>
    public void SetHandled() => IsHandled = true;
}

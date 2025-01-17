﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Represents a mediator command that is used to validate whether the subject is active or not.
/// </summary>
[PublicAPI]
public readonly record struct ValidateSubjectIsActiveCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    IOpenIdRequest OpenIdRequest,
    SubjectAuthentication SubjectAuthentication,
    ValidateSubjectIsActiveResult Result
) : ICommand;

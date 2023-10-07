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

using NIdentity.OpenId.Messages;

namespace NIdentity.OpenId.Results;

// TODO: register in DI (but we have a circular dependency)

internal class OpenIdErrorFactory : IOpenIdErrorFactory
{
    private IOpenIdMessageContext OpenIdMessageContext { get; }

    public OpenIdErrorFactory(IOpenIdMessageContext openIdMessageContext) =>
        OpenIdMessageContext = openIdMessageContext;

    public IOpenIdError Create(string errorCode) =>
        new OpenIdError(OpenIdMessageContext, errorCode);
}

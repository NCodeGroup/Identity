#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Results;

public abstract class BaseResult
{
    public bool IsNoResult { get; protected set; }

    [MemberNotNullWhen(true, nameof(ErrorDetails))]
    public bool IsError => ErrorDetails is not null;

    public IErrorDetails? ErrorDetails { get; protected set; }
}

public abstract class BaseResult<T> : BaseResult
    where T : BaseResult<T>, new()
{
    public static T NoResult() => new() { IsNoResult = true };

    public static T Error(IErrorDetails errorDetails) => new() { ErrorDetails = errorDetails };
}

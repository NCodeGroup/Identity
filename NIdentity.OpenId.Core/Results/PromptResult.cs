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

namespace NIdentity.OpenId.Results;

public class PromptResult : BaseResult<PromptResult>
{
    [MemberNotNullWhen(true, nameof(LoginReason))]
    public bool IsLoginRequired => !string.IsNullOrEmpty(LoginReason);

    public string? LoginReason { get; private init; }

    public bool IsConsentRequired { get; private init; }

    public bool IsCreateAccountRequired { get; private init; }

    [MemberNotNullWhen(true, nameof(RedirectUri))]
    public bool IsRedirectRequired => RedirectUri is not null;

    public Uri? RedirectUri { get; private init; }

    //

    public static PromptResult LoginRequired(string loginReason) => new() { LoginReason = loginReason };
    public static PromptResult ConsentRequired() => new() { IsConsentRequired = true };
    public static PromptResult CreateAccountRequired() => new() { IsCreateAccountRequired = true };
    public static PromptResult RedirectRequired(Uri redirectUri) => new() { RedirectUri = redirectUri };
}

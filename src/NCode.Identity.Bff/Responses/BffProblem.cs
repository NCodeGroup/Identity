#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Bff.Responses;

/// <summary>
/// The <c>application/problem+json</c> body of a BFF API refusal.
/// </summary>
/// <param name="Type">
/// The problem type, one of the <see cref="BffProblemTypes"/> URNs.
/// </param>
/// <param name="Title">
/// The short, human-readable summary of the problem.
/// </param>
/// <param name="Status">
/// The HTTP status code of the response.
/// </param>
internal sealed record BffProblem(string Type, string Title, int Status);

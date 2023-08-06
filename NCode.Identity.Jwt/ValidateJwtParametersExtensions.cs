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

namespace NCode.Identity.Jwt;

public static class ValidateJwtParametersExtensions
{
    public static ValidateJwtParameters AddValidator(
        this ValidateJwtParameters parameters,
        IValidateJwtHandler handler)
    {
        parameters.Handlers.Add(handler);
        return parameters;
    }

    public static ValidateJwtParameters ValidateClaim(
        this ValidateJwtParameters parameters,
        string claimName,
        bool allowCollection,
        params string[] validValues) =>
        parameters.AddValidator(
            new ValidateClaimValueJwtHandler(claimName, allowCollection, validValues));

    public static ValidateJwtParameters ValidateIssuer(
        this ValidateJwtParameters parameters,
        params string[] validIssuers) =>
        parameters.ValidateClaim("iss", allowCollection: false, validIssuers);

    public static ValidateJwtParameters ValidateAudience(
        this ValidateJwtParameters parameters,
        params string[] validAudiences) =>
        parameters.ValidateClaim("aud", allowCollection: true, validAudiences);
}

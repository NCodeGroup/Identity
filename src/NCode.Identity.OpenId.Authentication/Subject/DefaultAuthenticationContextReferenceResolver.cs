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

using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a default implementation of <see cref="IAuthenticationContextReferenceResolver"/> that derives the
/// <c>acr</c> value from the satisfied authentication methods: multiple factors resolve to the PAPE multi-factor policy
/// URN (<see cref="OpenIdProtocol.AuthenticationContextClassReferences.MultiFactor"/>), otherwise the configured
/// <c>default_acr</c> baseline is asserted (and the claim is omitted when no baseline is configured). A deployment with
/// its own authentication-context policy registers a replacement implementation.
/// </summary>
internal sealed class DefaultAuthenticationContextReferenceResolver
    : IAuthenticationContextReferenceResolver
{
    private static readonly HashSet<string> KnowledgeFactors = new(StringComparer.Ordinal)
    {
        OpenIdProtocol.AuthenticationMethods.Password,
        OpenIdProtocol.AuthenticationMethods.PersonalIdentificationOrPattern,
        OpenIdProtocol.AuthenticationMethods.KnowledgeBasedAuthentication,
    };

    private static readonly HashSet<string> PossessionOrBiometricFactors = new(
        StringComparer.Ordinal
    )
    {
        OpenIdProtocol.AuthenticationMethods.OneTimePassword,
        OpenIdProtocol.AuthenticationMethods.ProofOfPossessionHardwareSecuredKey,
        OpenIdProtocol.AuthenticationMethods.ProofOfPossessionSoftwareSecuredKey,
        OpenIdProtocol.AuthenticationMethods.ConfirmationBySms,
        OpenIdProtocol.AuthenticationMethods.ConfirmationByTelephone,
        OpenIdProtocol.AuthenticationMethods.SmartCard,
        OpenIdProtocol.AuthenticationMethods.FacialRecognition,
        OpenIdProtocol.AuthenticationMethods.FingerprintBiometric,
        OpenIdProtocol.AuthenticationMethods.IrisScanBiometric,
        OpenIdProtocol.AuthenticationMethods.RetinaScanBiometric,
        OpenIdProtocol.AuthenticationMethods.VoiceBiometric,
    };

    /// <inheritdoc />
    public ValueTask<string?> ResolveAsync(
        AuthenticationContextReferenceRequest request,
        CancellationToken cancellationToken
    )
    {
        if (IndicatesMultiFactor(request.AuthenticationMethods))
        {
            return ValueTask.FromResult<string?>(
                OpenIdProtocol.AuthenticationContextClassReferences.MultiFactor
            );
        }

        var acr =
            request.Settings.TryGetValue(OpenIdSettingKeys.DefaultAcr, out var defaultAcr)
            && !string.IsNullOrEmpty(defaultAcr)
                ? defaultAcr
                : null;
        return ValueTask.FromResult(acr);
    }

    private static bool IndicatesMultiFactor(IReadOnlyCollection<string> methods)
    {
        // An explicit 'mfa' marker, or the combination of a knowledge factor with a possession/biometric factor,
        // indicates multi-factor authentication.
        if (methods.Contains(OpenIdProtocol.AuthenticationMethods.MultiFactorAuthentication))
        {
            return true;
        }

        var hasKnowledge = false;
        var hasPossessionOrBiometric = false;
        foreach (var method in methods)
        {
            if (KnowledgeFactors.Contains(method))
            {
                hasKnowledge = true;
            }
            else if (PossessionOrBiometricFactors.Contains(method))
            {
                hasPossessionOrBiometric = true;
            }
        }

        return hasKnowledge && hasPossessionOrBiometric;
    }
}

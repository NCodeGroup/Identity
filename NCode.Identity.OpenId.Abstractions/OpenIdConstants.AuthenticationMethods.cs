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

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> values that can be used in the <c>amr</c> claim.
    /// https://datatracker.ietf.org/doc/html/rfc8176#section-2
    /// </summary>
    public static class AuthenticationMethods
    {
        /// <summary>
        /// Biometric authentication [RFC4949] using facial recognition.
        /// </summary>
        public const string FacialRecognition = "face";

        /// <summary>
        /// Biometric authentication [RFC4949] using a fingerprint.
        /// </summary>
        public const string FingerprintBiometric = "fpt";

        /// <summary>
        /// Use of geolocation information for authentication, such as that provided by [W3C.REC-geolocation-API-20161108].
        /// </summary>
        public const string Geolocation = "geo";

        /// <summary>
        /// Proof-of-Possession (PoP) of a hardware-secured key. See Appendix C of [RFC4211] for a discussion on PoP.
        /// </summary>
        public const string ProofOfPossessionHardwareSecuredKey = "hwk";

        /// <summary>
        /// Biometric authentication [RFC4949] using an iris scan.
        /// </summary>
        public const string IrisScanBiometric = "iris";

        /// <summary>
        /// Knowledge-based authentication [NIST.800-63-2] [ISO29115].
        /// </summary>
        public const string KnowledgeBasedAuthentication = "kba";

        /// <summary>
        /// Multiple-channel authentication [MCA]. The authentication involves communication over more than one distinct
        /// communication channel. For instance, a multiple-channel authentication might involve both entering information into
        /// a workstation's browser and providing information on a telephone call to a pre-registered number.
        /// </summary>
        public const string MultipleChannelAuthentication = "mca";

        /// <summary>
        /// Multiple-factor authentication [NIST.800-63-2] [ISO29115]. When this is present, specific authentication methods
        /// used may also be included.
        /// </summary>
        public const string MultiFactorAuthentication = "mfa";

        /// <summary>
        /// One-time password [RFC4949].  One-time password specifications that this authentication method applies to include
        /// [RFC4226] and [RFC6238].
        /// </summary>
        public const string OneTimePassword = "otp";

        /// <summary>
        /// Personal Identification Number (PIN) [RFC4949] or pattern (not restricted to containing only numbers) that a user
        /// enters to unlock a key on the device.  This mechanism should have a way to deter an attacker from obtaining the PIN
        /// by trying repeated guesses.
        /// </summary>
        public const string PersonalIdentificationOrPattern = "pin";

        /// <summary>
        /// Password-based authentication [RFC4949].
        /// </summary>
        public const string Password = "pwd";

        /// <summary>
        /// Risk-based authentication [JECM].
        /// </summary>
        public const string RiskBasedAuthentication = "rba";

        /// <summary>
        /// Biometric authentication [RFC4949] using a retina scan.
        /// </summary>
        public const string RetinaScanBiometric = "retina";

        /// <summary>
        /// Smart card [RFC4949].
        /// </summary>
        public const string SmartCard = "sc";

        /// <summary>
        /// Confirmation using SMS [SMS] text message to the user at a registered number.
        /// </summary>
        public const string ConfirmationBySms = "sms";

        /// <summary>
        /// Proof-of-Possession (PoP) of a software-secured key. See Appendix C of [RFC4211] for a discussion on PoP.
        /// </summary>
        public const string ProofOfPossessionSoftwareSecuredKey = "swk";

        /// <summary>
        /// Confirmation by telephone call to the user at a registered number. This authentication technique is sometimes also
        /// referred to as "call back" [RFC4949].
        /// </summary>
        public const string ConfirmationByTelephone = "tel";

        /// <summary>
        /// User presence test. Evidence that the end user is present and interacting with the device. This is sometimes also
        /// referred to as "test of user presence" [W3C.WD-webauthn-20170216].
        /// </summary>
        public const string UserPresenceTest = "user";

        /// <summary>
        /// Biometric authentication [RFC4949] using a voiceprint.
        /// </summary>
        public const string VoiceBiometric = "vbm";

        /// <summary>
        /// Windows integrated authentication [MSDN].
        /// </summary>
        public const string WindowsIntegratedAuthentication = "wia";
    }
}

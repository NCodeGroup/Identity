using NCode.Identity.Validation;

namespace NCode.Identity.Logic
{
    public interface IJwtDecoder
    {
        bool TryDecode(string jwt, string issuer, string audience, ISecurityKeyCollection securityKeys, string useErrorCode, out ValidationResult<string> result);
    }
}

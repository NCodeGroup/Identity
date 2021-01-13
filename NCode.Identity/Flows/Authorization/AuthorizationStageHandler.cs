using System.Threading;
using System.Threading.Tasks;
using NCode.Identity.Validation;

namespace NCode.Identity.Flows.Authorization
{
    public interface IAuthorizationStageHandler
    {
        AuthorizationStage Stage { get; }

        ValueTask<ValidationResult> HandleAsync(AuthorizationContext context, CancellationToken cancellationToken);
    }

    public abstract class AuthorizationStageHandler : IAuthorizationStageHandler
    {
        public abstract AuthorizationStage Stage { get; }

        public virtual ValueTask<ValidationResult> HandleAsync(AuthorizationContext context, CancellationToken cancellationToken)
        {
            var result = Handle(context);
            return ValueTask.FromResult(result);
        }

        public abstract ValidationResult Handle(AuthorizationContext context);
    }
}

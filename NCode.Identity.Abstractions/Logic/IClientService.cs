using System.Threading;
using System.Threading.Tasks;
using NCode.Identity.DataContracts;

namespace NCode.Identity.Logic
{
    public interface IClientService
    {
        ValueTask<Client> GetClientAsync(string clientId, CancellationToken cancellationToken);
    }
}

using System.Threading;
using System.Threading.Tasks;
using NCode.Identity.Repository.DataContracts;

namespace NCode.Identity.Repository
{
    public interface IClientRepository
    {
        ValueTask<Client> GetClientAsync(string clientId, CancellationToken cancellationToken);
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace Raft.Server
{
    public interface IPlanner
    {
        Task HeatbeatDelay();
        Task ElectionDelay();

        Task RetryDelay();
    }
}
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Interfaces
{
    public interface IState
    {
        Task<IState> Execute(ISession session, CancellationToken cancellationToken);
    }
}
using System.Threading.Tasks;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Messages;

namespace ServiceTrashInspectionPlugin.Handlers
{
    public class EformCompletedHandler : IHandleMessages<EformCompleted>
    {
        public async Task Handle(EformCompleted message)
        {
            throw new System.NotImplementedException();
        }
    }
}
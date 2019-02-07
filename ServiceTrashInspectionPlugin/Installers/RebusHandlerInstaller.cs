using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Messages;
using ServiceTrashInspectionPlugin.Handlers;

namespace ServiceTrashInspectionPlugin.Installers
{
    public class RebusHandlerInstaller : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
        container.Register(Component.For<IHandleMessages<EformCompleted>>().ImplementedBy<EformCompletedHandler>().LifestyleTransient());

        }
    }
}
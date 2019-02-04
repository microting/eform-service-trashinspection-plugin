using Rebus.Bus;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Microting.WindowsService.BasePn;
using System.ComponentModel.Composition;
using System.IO;
using System.Collections.Generic;
using System;

namespace ServiceTrashInspectionPlugin
{
    [Export(typeof(ISdkEventHandler))]
    public class Core : ISdkEventHandler
    {
        public void CaseCompleted(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void CaseDeleted(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void CoreEventException(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void eFormProcessed(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void eFormProcessingError(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void eFormRetrived(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public void NotificationNotFound(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }

        public bool Restart(int sameExceptionCount, int sameExceptionCountMax, bool shutdownReallyFast)
        {
            throw new NotImplementedException();
        }

        public bool Start(string sdkConnectionString, string serviceLocation)
        {
            throw new NotImplementedException();
        }

        public bool Stop(bool shutdownReallyFast)
        {
            throw new NotImplementedException();
        }

        public void UnitActivated(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}

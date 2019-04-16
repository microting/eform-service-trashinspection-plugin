using Rebus.Bus;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Microting.WindowsService.BasePn;
using System.ComponentModel.Composition;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using ServiceTrashInspectionPlugin.Installers;
using ServiceTrashInspectionPlugin.Messages;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Factories;
using Microsoft.EntityFrameworkCore;

namespace ServiceTrashInspectionPlugin
{
    [Export(typeof(ISdkEventHandler))]
    public class Core : ISdkEventHandler
    {
        #region var

//        private SqlController _sqlController;
        private Tools t = new Tools();
        private eFormCore.Core _sdkCore;
//        public Log log;
        private IWindsorContainer _container;
        public IBus _bus;
        private bool _coreThreadRunning = false;
        private bool _coreRestarting = false;
        private bool _coreStatChanging = false;
        private bool _coreAvailable = false;
        private string _serviceLocation;
        private int _maxParallelism = 1;
        private int _numberOfWorkers = 1;
        private TrashInspectionPnDbContext _dbContext;
        #endregion

        public void CaseCompleted(object sender, EventArgs args)
        {
            eFormShared.Case_Dto trigger = (eFormShared.Case_Dto)sender;

            string CaseId = trigger.MicrotingUId;
            _bus.SendLocal(new eFormCompleted(CaseId));
        }

        public void CaseDeleted(object sender, EventArgs args)
        {
            // Do nothing
        }

        public void CoreEventException(object sender, EventArgs args)
        {
            // Do nothing
        }

        public void eFormProcessed(object sender, EventArgs args)
        {
            // Do nothing
        }

        public void eFormProcessingError(object sender, EventArgs args)
        {
            // Do nothing
        }

        public void eFormRetrived(object sender, EventArgs args)
        {
            eFormShared.Case_Dto trigger = (eFormShared.Case_Dto)sender;

            string CaseId = trigger.MicrotingUId;
            _bus.SendLocal(new eFormRetrieved(CaseId));
        }

        public void NotificationNotFound(object sender, EventArgs args)
        {
            // Do nothing
        }

        public bool Restart(int sameExceptionCount, int sameExceptionCountMax, bool shutdownReallyFast)
        {
            return true;
        }

        public bool Start(string sdkConnectionString, string serviceLocation)
        {
            Console.WriteLine("TrashInspectionPlugin start called");
            try
            {
                string dbNameSection;
                string dbPrefix;
                if (sdkConnectionString.ToLower().Contains("convert zero datetime"))
                {
                    dbNameSection = Regex.Match(sdkConnectionString, @"(Database=\w*;)").Groups[0].Value;
                    dbPrefix = Regex.Match(sdkConnectionString, @"Database=(\d*)_").Groups[1].Value;
                } else
                {
                    dbNameSection = Regex.Match(sdkConnectionString, @"(Initial Catalog=\w*;)").Groups[0].Value;
                    dbPrefix = Regex.Match(sdkConnectionString, @"Initial Catalog=(\d*)_").Groups[1].Value;
                }
                
                
                string pluginDbName = $"Initial Catalog={dbPrefix}_eform-angular-trashinspection-plugin;";
                string connectionString = sdkConnectionString.Replace(dbNameSection, pluginDbName);


                if (!_coreAvailable && !_coreStatChanging)
                {
                    _serviceLocation = serviceLocation;
                    _coreStatChanging = true;
                    
                    if (string.IsNullOrEmpty(_serviceLocation))
                        throw new ArgumentException("serviceLocation is not allowed to be null or empty");

                    if (string.IsNullOrEmpty(connectionString))
                        throw new ArgumentException("serverConnectionString is not allowed to be null or empty");

                    TrashInspectionPnContextFactory contextFactory = new TrashInspectionPnContextFactory();

                    _dbContext = contextFactory.CreateDbContext(new[] { connectionString });
                    _dbContext.Database.Migrate();

                    _coreAvailable = true;
                    _coreStatChanging = false;

                    startSdkCoreSqlOnly(sdkConnectionString);

                    _container = new WindsorContainer();
                    _container.Register(Component.For<TrashInspectionPnDbContext>().Instance(_dbContext));
                    _container.Register(Component.For<eFormCore.Core>().Instance(_sdkCore));
                    _container.Install(
                        new RebusHandlerInstaller()
                        , new RebusInstaller(connectionString, _maxParallelism, _numberOfWorkers)
                    );


                    _bus = _container.Resolve<IBus>();
                }
                Console.WriteLine("TrashInspectionPlugin started");
                return true;
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Start failed " + ex.Message);
                throw ex;
            }
        }

        public bool Stop(bool shutdownReallyFast)
        {
            
            try
            {
                if (_coreAvailable && !_coreStatChanging)
                {
                    _coreStatChanging = true;

                    _coreAvailable = false;

                    int tries = 0;
                    while (_coreThreadRunning)
                    {
                        Thread.Sleep(100);
                        _bus.Dispose();
                        tries++;
                    }
                    _sdkCore.Close();

                    _coreStatChanging = false;
                }
            }
            catch (ThreadAbortException)
            {
                //"Even if you handle it, it will be automatically re-thrown by the CLR at the end of the try/catch/finally."
                Thread.ResetAbort(); //This ends the re-throwning
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return true;
        }

        public void UnitActivated(object sender, EventArgs args)
        {
            throw new NotImplementedException();
        }
        
        public void startSdkCoreSqlOnly(string sdkConnectionString)
        {
            _sdkCore = new eFormCore.Core();

            _sdkCore.StartSqlOnly(sdkConnectionString);
        }
    }
}

using Rebus.Bus;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Microting.WindowsService.BasePn;
using System.ComponentModel.Composition;
using System.IO;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
//using System.Runtime.InteropServices;
using System.Threading;
//using eFormCore.Installers;
//using ServiceTrashInspectionPlugin.Infrastructure;
using ServiceTrashInspectionPlugin.Installers;
using ServiceTrashInspectionPlugin.Messages;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Factories;
using Microsoft.EntityFrameworkCore;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Factories.Factories;

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
                var dbNameSection = Regex.Match(sdkConnectionString, @"(Database=\w*;)").Groups[0].Value;
                var dbPrefix = Regex.Match(sdkConnectionString, @"Database=(\d*)_").Groups[1].Value;
                
                var pluginDbName = $"Database={dbPrefix}_EFormTrashInspectionPn;";
                string connectionString = sdkConnectionString.Replace(dbNameSection, pluginDbName);

//                string connectionString;// = sdkConnectionString;
                //if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                //{
//                connectionString =
//                    File.ReadAllText(serviceLocation + @"/Plugins/TrashInspection/netstandard2.0/sql_connection.txt")
//                        .Trim();
                //}
                //else
                //{
                //connectionString =
                //    File.ReadAllText(serviceLocation + @"\Plugins\TrashInspection\sql_connection.txt")
                //        .Trim();
                //}

                if (!_coreAvailable && !_coreStatChanging)
                {
                    _serviceLocation = serviceLocation;
                    _coreStatChanging = true;
                    
                    if (string.IsNullOrEmpty(_serviceLocation))
                        throw new ArgumentException("serviceLocation is not allowed to be null or empty");

                    if (string.IsNullOrEmpty(connectionString))
                        throw new ArgumentException("serverConnectionString is not allowed to be null or empty");

                    //sqlController
                    //                    _sqlController = new SqlController(connectionString);


                    //check settings
                    //                    if (_sqlController.SettingCheckAll().Count > 0)
                    //                        throw new ArgumentException("Use AdminTool to setup database correctly. 'SettingCheckAll()' returned with errors");

                    //                    if (_sqlController.SettingRead(SqlController.Settings.SdkConnectionString) == "...")
                    //                        throw new ArgumentException("Use AdminTool to setup database correctly. microtingDb(connection string) not set, only default value found");
                    //                    
                    //                    try
                    //                    {
                    //                        _maxParallelism = int.Parse(_sqlController.SettingRead(SqlController.Settings.MaxParallelism));
                    //                        _numberOfWorkers = int.Parse(_sqlController.SettingRead(SqlController.Settings.NumberOfWorkers));
                    //                    }
                    //                    catch { }
                    TrashInspectionPnContextFactory contextFactory = new TrashInspectionPnContextFactory();

                    _dbContext = contextFactory.CreateDbContext(new[] { connectionString });
                    _dbContext.Database.Migrate();

                    _coreAvailable = true;
                    _coreStatChanging = false;

                    //                    string sdkCoreConnectionString = _sqlController.SettingRead(SqlController.Settings.SdkConnectionString);
                    startSdkCoreSqlOnly(sdkConnectionString);

                    _container = new WindsorContainer();
                    //                    _container.Register(Component.For<SqlController>().Instance(_sqlController));
                    _container.Register(Component.For<TrashInspectionPnDbContext>().Instance(_dbContext));
                    _container.Register(Component.For<eFormCore.Core>().Instance(_sdkCore));
//                    _container.Register(Component.For<Log>().Instance(log));
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
//                log.LogException(t.GetMethodName("Core"), "Start failed", ex, false);
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
//                    log.LogCritical(t.GetMethodName("Core"), "called");

                    int tries = 0;
                    while (_coreThreadRunning)
                    {
                        Thread.Sleep(100);
                        _bus.Dispose();
                        tries++;
                    }

//                    log.LogStandard(t.GetMethodName("Core"), "Core closed");
//                    _sqlController = null;
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
//                log.LogException(t.GetMethodName("Core"), "Core failed to close", ex, false);
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

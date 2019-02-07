using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using eFormSqlController;
using Microsoft.EntityFrameworkCore;

namespace ServiceTrashInspectionPlugin.Infrastructure
{
    public class SqlController
    {
        
        Tools t = new Tools();

        object _writeLock = new object();

        string connectionStr;

        
        #region con
        public SqlController(string connectionString)
        {
            connectionStr = connectionString;          

            #region migrate if needed
            try
            {
                using (var db = GetContext())
                {
                    //TODO! THIS part need to be redone in some form in EF Core!
                   
                    db.Database.Migrate();
                    db.Database.EnsureCreated();

                    var match = db.settings.Count();
                }
            }
            catch
            {
                //TODO! THIS part need to be redone in some form in EF Core!
                // MigrateDb();
            }
            #endregion

            //region set default for settings if needed
            if (SettingCheckAll().Count > 0)
                SettingCreateDefaults();
        }

        private MicrotingDbAnySql GetContext()
        {

            DbContextOptionsBuilder dbContextOptionsBuilder = new DbContextOptionsBuilder();

            if (connectionStr.ToLower().Contains("convert zero datetime"))
            {
                dbContextOptionsBuilder.UseMySql(connectionStr);
            }
            else
            {
                dbContextOptionsBuilder.UseSqlServer(connectionStr);
            }
            dbContextOptionsBuilder.UseLazyLoadingProxies(true);
            return new MicrotingDbAnySql(dbContextOptionsBuilder.Options);

        }

        public bool MigrateDb()
        {
            //var configuration = new Configuration();
            //configuration.TargetDatabase = new DbConnectionInfo(connectionStr, "System.Data.SqlClient");
            //var migrator = new DbMigrator(configuration);

            //migrator.Update();
            return true;
        }
        #endregion
        
        
        #region public setting
        public bool SettingCreateDefaults()
        {
            //key point
            SettingCreate(Settings.SdkConnectionString);
            SettingCreate(Settings.LogLevel);
            SettingCreate(Settings.LogLimit);
            SettingCreate(Settings.MaxParallelism);
            SettingCreate(Settings.NumberOfWorkers);

            return true;
        }

        public bool SettingCreate(Settings name)
        {
            using (var db = GetContext())
            {
                //key point
                #region id = settings.name
                int id = -1;
                string defaultValue = "default";
                switch (name)
                {
                    case Settings.SdkConnectionString: defaultValue = "..."; break;
                    case Settings.LogLevel: defaultValue = "4"; break;
                    case Settings.LogLimit: defaultValue = "25000"; break;
                    case Settings.MaxParallelism: defaultValue = "1"; break;
                    case Settings.NumberOfWorkers: defaultValue = "1"; break;
                    
                    default:
                        throw new IndexOutOfRangeException(name.ToString() + " is not a known/mapped Settings type");
                }
                #endregion

                settings matchId = db.settings.SingleOrDefault(x => x.id == id);
                settings matchName = db.settings.SingleOrDefault(x => x.name == name.ToString());

                if (matchName == null)
                {
                    if (matchId != null)
                    {
                        #region there is already a setting with that id but different name
                        //the old setting data is copied, and new is added
                        settings newSettingBasedOnOld = new settings();
                        newSettingBasedOnOld.id = (db.settings.Select(x => (int?)x.id).Max() ?? 0) + 1;
                        newSettingBasedOnOld.name = matchId.name.ToString();
                        newSettingBasedOnOld.value = matchId.value;

                        db.settings.Add(newSettingBasedOnOld);

                        matchId.name = name.ToString();
                        matchId.value = defaultValue;

                        db.SaveChanges();
                        #endregion
                    }
                    else
                    {
                        //its a new setting
                        settings newSetting = new settings();
                        newSetting.id = id;
                        newSetting.name = name.ToString();
                        newSetting.value = defaultValue;

                        db.settings.Add(newSetting);
                    }
                    db.SaveChanges();
                }
                else
                    if (string.IsNullOrEmpty(matchName.value))
                    matchName.value = defaultValue;
            }

            return true;
        }

        public string SettingRead(Settings name)
        {
            try
            {
                using (var db = GetContext())
                {
                    settings match = db.settings.Single(x => x.name == name.ToString());

                    if (match.value == null)
                        return "";

                    return match.value;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(t.GetMethodName("SQLController") + " failed", ex);
            }
        }

        public void SettingUpdate(Settings name, string newValue)
        {
            try
            {
                using (var db = GetContext())
                {
                    settings match = db.settings.SingleOrDefault(x => x.name == name.ToString());

                    if (match == null)
                    {
                        SettingCreate(name);
                        match = db.settings.Single(x => x.name == name.ToString());
                    }

                    match.value = newValue;
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(t.GetMethodName("SQLController") + " failed", ex);
            }
        }

        public List<string> SettingCheckAll()
        {
            List<string> result = new List<string>();
            try
            {
                using (var db = GetContext())
                {
                    

                    int countVal = db.settings.Count(x => x.value == "");
                    int countSet = db.settings.Count();

                    if (countSet == 0)
                    {
                        result.Add("NO SETTINGS PRESENT, NEEDS PRIMING!");
                        return result;
                    }

                    foreach (var setting in Enum.GetValues(typeof(Settings)))
                    {
                        try
                        {
                            string readSetting = SettingRead((Settings)setting);
                            if (string.IsNullOrEmpty(readSetting))
                                result.Add(setting.ToString() + " has an empty value!");
                        }
                        catch
                        {
                            result.Add("There is no setting for " + setting + "! You need to add one");
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(t.GetMethodName("SQLController") + " failed", ex);
            }
        }
        #endregion
        
        public enum Settings
        {
            LogLevel,
            LogLimit,
            SdkConnectionString,
            MaxParallelism,
            NumberOfWorkers
        }
    }
}
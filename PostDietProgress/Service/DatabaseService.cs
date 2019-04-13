﻿using Dapper;
using PostDietProgress.Model;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace PostDietProgress.Service
{
    public class DatabaseService
    {
        Settings Setting;
        public DatabaseService(Settings setting)
        {
            Setting = setting;
        }


        public void CreateTable()
        {
            /* データ保管用テーブル作成 */
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (var cmd = dbConn.CreateCommand())
                {

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS [SETTING] (" +
                                                          "[KEY]  TEXT NOT NULL," +
                                                          "[VALUE] TEXT NOT NULL" +
                                                          ");";
                    cmd.ExecuteNonQuery();

                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("INSERT INTO SETTING (KEY,VALUE) SELECT @Key, @Val WHERE NOT EXISTS(SELECT 1 FROM SETTING WHERE KEY = @Key)");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "OAUTHTOKEN", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "ACCESSTOKEN", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = "" }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        public string GetPreviousDate()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                try
                {
                    using (var cmd = dbConn.CreateCommand())
                    {
                        var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'PREVIOUSMEASUREMENTDATE'").FirstOrDefault();

                        return dbObj?.Value;
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        public string GetOAuthToken()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'OAUTHTOKEN'").FirstOrDefault();

                return dbObj == null ? null : (string.IsNullOrEmpty(dbObj.Value) ? null : dbObj.Value);
            }
        }

        public void SetOAuthToken()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (SQLiteCommand cmd = dbConn.CreateCommand())
                {
                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "OAUTHTOKEN", Val = Setting.TanitaOAuthToken }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        public string GetAccessToken()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'ACCESSTOKEN'").FirstOrDefault();

                return dbObj == null ? null : (string.IsNullOrEmpty(dbObj.Value) ? null : dbObj.Value);
            }
        }

        public void SetAccessToken()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (dbConn.CreateCommand())
                {
                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "ACCESSTOKEN", Val = Setting.TanitaAccessToken }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        public void SetHealthData(string latestDate,Dictionary<string,string> latestHealthData)
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (var cmd = dbConn.CreateCommand())
                {
                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = latestDate }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = latestHealthData[((int)HealthTag.WEIGHT).ToString()] }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            return;
                        }
                    }
                }
            }
        }

        public string GetPreviousData()
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'PREVIOUSWEIGHT'").FirstOrDefault();

                return dbObj?.Value;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using MySql.Data.MySqlClient;
using MySqlModule.Models.MySql;
using TCAdmin.SDK.Objects;
using TCAdmin.SDK.Web.MVC.Controllers;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace MySqlModule.Controllers
{
    [Authorize]
    public class MySqlController : BaseController
    {
        [HttpGet]
        public ActionResult Index()
        {
            var model = new MySqlModel
            {
                CurrentDatabases = GetUserDatabases().Count,
                MaxDatabases = GetUserServicesCount(),
                CreationServiceIds = GetUserServices(),
                EligibleLocations = GetLocations(),
                CreationUsername = GetDbUsername(),
                DeletionUsernames = GetDbDeletionUsernames(),
                ResetUsernames = GetDbResetUsernames(),
                MigrationDbCount = GetOldUserDatabases()
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult DatabasesGrid()
        {
            return PartialView("_Databases");
        }

        [HttpPost]
        [ParentAction("MySql", "Index")]
        public ActionResult DatabasesByUserRead([DataSourceRequest] DataSourceRequest request)
        {
            var databases = GetUserDatabases();
            return Json(databases.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }

        public ActionResult CreateDatabase(int requestServiceId1, string requestDbName)
        {
            if (GetUserDatabases().Count >= GetUserServicesCount())
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'You have reached your database limit!');$('body').css('cursor', 'default');");
            }

            if (requestDbName == string.Empty)
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'You did not enter a database name into the textbox!');$('body').css('cursor', 'default');");
            }

            if (!Regex.IsMatch(requestDbName, "^[_a-zA-Z0-9 ]*$"))
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'That database name is not allowed due to illegal characters!');$('body').css('cursor', 'default');");
            }

            if (requestDbName.Length > 64)
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'That database name is too long!');$('body').css('cursor', 'default');");
            }


            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false).Cast<Service>().ToList();

            var service = services.Find(x => x.ServiceId == requestServiceId1);
            if (service == null)
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'You don't own this service');$('body').css('cursor', 'default');");
            }

            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                var dbUser = $"{user.UserName}_{service.ServiceId}";
                var dbName = $"{user.UserName}_{requestDbName.Replace(" ", "_")}";
                var dbPass = System.Web.Security.Membership.GeneratePassword(12, 0);
                
                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var createDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                           datacenter.MySqlPluginRoot + ";password=" +
                                                           datacenter.MySqlPluginPassword + ";");
                        createDb.Open();
                        if (createDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(createDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var cmd = createDb.CreateCommand();
                                var createDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "GRANT USAGE on *.* to '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL PRIVILEGES ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = createDbSql;
                                cmd.ExecuteNonQuery();
                                createDb.Close();
                            }
                            else
                            {
                                var cmd = createDb.CreateCommand();
                                var createDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "CREATE USER '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = createDbSql;
                                cmd.ExecuteNonQuery();
                                createDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Host"] = datacenter.MySqlPluginIp;
                        service.Variables["_MySQLPlugin::Username"] = dbUser;
                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                        service.Variables["_MySQLPlugin::Database"] = dbName;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var createDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                           server.MySqlPluginRoot + ";password=" +
                                                           server.MySqlPluginPassword +
                                                           ";");
                        createDb.Open();
                        if (createDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(createDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var cmd = createDb.CreateCommand();
                                var createDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "GRANT USAGE on *.* to '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL PRIVILEGES ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = createDbSql;
                                cmd.ExecuteNonQuery();
                                createDb.Close();
                            }
                            else
                            {
                                var cmd = createDb.CreateCommand();
                                var createDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "CREATE USER '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = createDbSql;
                                cmd.ExecuteNonQuery();
                                createDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Host"] = server.MySqlPluginIp;
                        service.Variables["_MySQLPlugin::Username"] = dbUser;
                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                        service.Variables["_MySQLPlugin::Database"] = dbName;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }
                else
                {
                    return JavaScript(
                        "TCAdmin.Ajax.ShowBasicDialog('Error', 'An Administrator has not configured the location of this service for the MySql Module!');$('body').css('cursor', 'default');");
                }

                service.Save();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }

            return JavaScript("window.location.reload(false);");
        }

        public ActionResult DeleteDatabase(int requestServiceId2)
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false).Cast<Service>().ToList();

            var service = services.Find(x => x.ServiceId == requestServiceId2);
            if (service == null)
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', 'You don't own this service');$('body').css('cursor', 'default');");
            }

            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                var dbUser = service.Variables["_MySQLPlugin::Username"].ToString();
                var dbName = service.Variables["_MySQLPlugin::Database"].ToString();

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var deleteDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                           datacenter.MySqlPluginRoot + ";password=" +
                                                           datacenter.MySqlPluginPassword + ";");
                        deleteDb.Open();
                        var cmd = deleteDb.CreateCommand();
                        var deleteDbSql = string.Concat(
                            "DROP USER `" + dbUser + "`@'%';"
                            , " DROP DATABASE IF EXISTS `" + dbName + "`;");
                        cmd.CommandText = deleteDbSql;
                        cmd.ExecuteNonQuery();
                        deleteDb.Close();

                        service.Variables["_MySQLPlugin::Host"] = null;
                        service.Variables["_MySQLPlugin::Username"] = null;
                        service.Variables["_MySQLPlugin::Password"] = null;
                        service.Variables["_MySQLPlugin::Database"] = null;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var deleteDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                           server.MySqlPluginRoot + ";password=" +
                                                           server.MySqlPluginPassword + ";");
                        deleteDb.Open();
                        var cmd = deleteDb.CreateCommand();
                        var deleteDbSql = string.Concat(
                            "DROP USER " + dbUser + "@`%`;"
                            , " DROP DATABASE IF EXISTS `" + dbName + "`;");
                        cmd.CommandText = deleteDbSql;
                        cmd.ExecuteNonQuery();
                        deleteDb.Close();

                        service.Variables["_MySQLPlugin::Host"] = null;
                        service.Variables["_MySQLPlugin::Username"] = null;
                        service.Variables["_MySQLPlugin::Password"] = null;
                        service.Variables["_MySQLPlugin::Database"] = null;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }

                service.Save();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }

            return JavaScript("window.location.reload(false);");
        }

        public static void DeleteDatabase(Service service)
        {
            try
            {
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                
                if (!service.Variables.HasValue("_MySQLPlugin::Username") || string.IsNullOrEmpty(service.Variables["_MySQLPlugin::Username"].ToString()))
                {
                    return;
                }
                
                var dbUser = service.Variables["_MySQLPlugin::Username"].ToString();
                var dbName = service.Variables["_MySQLPlugin::Database"].ToString();

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    var deleteDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                       datacenter.MySqlPluginRoot + ";password=" +
                                                       datacenter.MySqlPluginPassword + ";");
                    deleteDb.Open();
                    var cmd = deleteDb.CreateCommand();
                    var deleteDbSql = string.Concat(
                        "DROP USER " + dbUser + "@`%`;"
                        , " DROP DATABASE IF EXISTS `" + dbName + "`;");
                    cmd.CommandText = deleteDbSql;
                    cmd.ExecuteNonQuery();
                    deleteDb.Close();
                    service.Variables["_MySQLPlugin::Host"] = null;
                    service.Variables["_MySQLPlugin::Username"] = null;
                    service.Variables["_MySQLPlugin::Password"] = null;
                    service.Variables["_MySQLPlugin::Database"] = null;
                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {
                    var deleteDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                       server.MySqlPluginRoot + ";password=" +
                                                       server.MySqlPluginPassword + ";");
                    deleteDb.Open();
                    var cmd = deleteDb.CreateCommand();
                    var deleteDbSql = string.Concat(
                        "DROP USER " + dbUser + "@`%`;"
                        , " DROP DATABASE IF EXISTS `" + dbName + "`;");
                    cmd.CommandText = deleteDbSql;
                    cmd.ExecuteNonQuery();
                    deleteDb.Close();
                    service.Variables["_MySQLPlugin::Host"] = null;
                    service.Variables["_MySQLPlugin::Username"] = null;
                    service.Variables["_MySQLPlugin::Password"] = null;
                    service.Variables["_MySQLPlugin::Database"] = null;
                }

                service.Save();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        public ActionResult ResetPassword(int requestServiceId3)
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false).Cast<Service>().ToList();

            var service = services.Find(x => x.ServiceId == requestServiceId3);
            if (service == null)
            {
                return JavaScript(
                    "TCAdmin.Ajax.ShowBasicDialog('Error', ''You don't own this service');$('body').css('cursor', 'default');");
            }

            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                var dbUser = service.Variables["_MySQLPlugin::Username"].ToString();
                var dbPass = System.Web.Security.Membership.GeneratePassword(12, 2);

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var resetDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                          datacenter.MySqlPluginRoot + ";password=" +
                                                          datacenter.MySqlPluginPassword + ";");
                        resetDb.Open();
                        if (resetDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(resetDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var cmd = resetDb.CreateCommand();
                                var resetDbSql = "SET PASSWORD FOR '" + dbUser + "' = PASSWORD('" + dbPass + "');";
                                cmd.CommandText = resetDbSql;
                                cmd.ExecuteNonQuery();
                                resetDb.Close();
                            }
                            else
                            {
                                var cmd = resetDb.CreateCommand();
                                var resetDbSql = "ALTER USER '" + dbUser + "' IDENTIFIED BY '" + dbPass + "';";
                                cmd.CommandText = resetDbSql;
                                cmd.ExecuteNonQuery();
                                resetDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var resetDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                          server.MySqlPluginRoot + ";password=" +
                                                          server.MySqlPluginPassword + ";");
                        resetDb.Open();
                        if (resetDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(resetDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var cmd = resetDb.CreateCommand();
                                var resetDbSql = "SET PASSWORD FOR '" + dbUser + "' = PASSWORD('" + dbPass + "');";
                                cmd.CommandText = resetDbSql;
                                cmd.ExecuteNonQuery();
                                resetDb.Close();
                            }
                            else
                            {
                                var cmd = resetDb.CreateCommand();
                                var resetDbSql = "ALTER USER '" + dbUser + "' IDENTIFIED BY '" + dbPass + "';";
                                cmd.CommandText = resetDbSql;
                                cmd.ExecuteNonQuery();
                                resetDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                    }
                    catch (Exception e)
                    {
                        return JavaScript(
                            $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                    }
                }

                service.Save();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }

            return JavaScript("window.location.reload(false);");
        }

        public ActionResult MigrateDatabases()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false).Cast<Service>().ToList();

            foreach (var service in services.Where(service => service.Variables.HasValue("MySQLUser")))
            {
                try
                {
                    service.Variables["_MySQLPlugin::Host"] = "localhost";
                    service.Variables["_MySQLPlugin::Username"] = service.Variables["MySQLUser"];
                    service.Variables["_MySQLPlugin::Database"] = service.Variables["MySQLUser"];
                    service.Variables["_MySQLPlugin::Password"] = service.Variables["MySQLPassword"];

                    service.Variables["MySQLUser"] = null;
                    service.Variables["MySQLPassword"] = null;
                }
                catch (Exception e)
                {
                    return JavaScript(
                        $"TCAdmin.Ajax.ShowBasicDialog('Error', 'Uh oh, something went wrong! Please contact an Administrator (see web console for details)!');console.log('{TCAdmin.SDK.Web.Utility.EscapeJavaScriptString(e.Message)}');$('body').css('cursor', 'default');");
                }

                service.Save();
                service.Configure();
            }

            return JavaScript("window.location.reload(false);");
        }

        public static void BackupDatabase(Service service)
        {
            try
            {
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                
                if (!service.Variables.HasValue("_MySQLPlugin::Database") || string.IsNullOrEmpty(service.Variables["_MySQLPlugin::Database"].ToString()))
                {
                    return;
                }

                var dbName = service.Variables["_MySQLPlugin::Database"].ToString();

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {

                    using (MySqlConnection backupDb = new MySqlConnection(
                        "server=" + datacenter.MySqlPluginIp + ";user=" +
                        datacenter.MySqlPluginRoot + ";password=" +
                        datacenter.MySqlPluginPassword + ";database=" + dbName + ";"))
                    {
                        backupDb.Open();
                        var cmd = backupDb.CreateCommand();
                        var sqlBackup = new MySqlBackup(cmd);
                        var alterDbSql = $"ALTER DATABASE `{dbName}` COLLATE latin1_swedish_ci;";
                        cmd.CommandText = alterDbSql;
                        cmd.ExecuteNonQuery();

                        const string tables = "SHOW TABLES;";
                        using (MySqlCommand tableCmd = new MySqlCommand(tables, backupDb))
                        {
                            var tableList = new List<string>();

                            using (MySqlDataReader reader = tableCmd.ExecuteReader())
                            {
                                if (reader != null)
                                {
                                    while (reader.Read())
                                    {
                                        tableList.Add(reader[$"Tables_in_{dbName}"].ToString());
                                    }
                                }
                            }
                            
                            foreach (var table in tableList)
                            {
                                var alterDbtable =
                                    $"ALTER TABLE {table} CONVERT TO CHARACTER SET latin1 COLLATE latin1_swedish_ci";
                                tableCmd.CommandText = alterDbtable;
                                tableCmd.ExecuteNonQuery();
                            }
                        }

                        Console.WriteLine(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        sqlBackup.ExportToFile(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        backupDb.Close();
                    }

                    DeleteDatabase(service);
                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {

                    using (MySqlConnection backupDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                                          server.MySqlPluginRoot + ";password=" +
                                                                          server.MySqlPluginPassword + ";database=" +
                                                                          dbName + ";"))
                    {

                        var cmd = backupDb.CreateCommand();
                        var sqlBackup = new MySqlBackup(cmd);
                        backupDb.Open();
                        var alterDbSql = $"ALTER DATABASE `{dbName}` COLLATE latin1_swedish_ci;";
                        cmd.CommandText = alterDbSql;
                        cmd.ExecuteNonQuery();

                        const string tables = "SHOW TABLES;";
                        using (MySqlCommand tableCmd = new MySqlCommand(tables, backupDb))
                        {
                            var tableList = new List<string>();

                            using (MySqlDataReader reader = tableCmd.ExecuteReader())
                            {
                                if (reader != null)
                                {
                                    while (reader.Read())
                                    {
                                        tableList.Add(reader[$"Tables_in_{dbName}"].ToString());
                                    }
                                }
                            }
                            
                            foreach (var table in tableList)
                            {
                                var alterDbtable =
                                    $"ALTER TABLE {table} CONVERT TO CHARACTER SET latin1 COLLATE latin1_swedish_ci";
                                tableCmd.CommandText = alterDbtable;
                                tableCmd.ExecuteNonQuery();
                            }
                        }

                        Console.WriteLine(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        sqlBackup.ExportToFile(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        backupDb.Close();
                    }

                    DeleteDatabase(service);
                }

                service.Save();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        public static void RestoreDatabase(Service service)
        {
            try
            {
                var server = new Server(service.ServerId);
                var datacenter = new Datacenter(server.DatacenterId);
                var dirInfo = new DirectoryInfo(service.RootDirectory);
                var files = dirInfo.GetFiles().Where(f => (f.Name.EndsWith(".sql")))
                    .OrderByDescending(p => p.CreationTime).ToArray();
                
                if (files.Length == 0)
                {
                    return;
                }
                
                var dbUser = $"{service.UserName}_{service.ServiceId}";
                var dbName = Path.GetFileNameWithoutExtension(files[0].Name);
                var dbPass = System.Web.Security.Membership.GeneratePassword(12, 2);

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    using (MySqlConnection restoreDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                             datacenter.MySqlPluginRoot + ";password=" +
                                                             datacenter.MySqlPluginPassword + ";"))
                    {


                        var cmd = restoreDb.CreateCommand();
                        restoreDb.Open();
                        if (restoreDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(restoreDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var restoreDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "GRANT USAGE on *.* to '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL PRIVILEGES ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = restoreDbSql;
                                cmd.ExecuteNonQuery();
                                restoreDb.Close();
                            }
                            else
                            {
                                var restoreDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "CREATE USER '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = restoreDbSql;
                                cmd.ExecuteNonQuery();
                                restoreDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Host"] = datacenter.MySqlPluginIp;
                        service.Variables["_MySQLPlugin::Username"] = dbUser;
                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                        service.Variables["_MySQLPlugin::Database"] = dbName;
                    }

                    using (MySqlConnection restoreDb2 = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                                 datacenter.MySqlPluginRoot + ";password=" +
                                                                 datacenter.MySqlPluginPassword + ";database=" +
                                                                 dbName +
                                                                 ";"))
                    {

                        var cmd2 = restoreDb2.CreateCommand();
                        var sqlRestore = new MySqlBackup(cmd2);
                        restoreDb2.Open();
                        sqlRestore.ImportFromFile(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        restoreDb2.Close();
                    }

                }
                else if (!server.MySqlPluginUseDatacenter && server.MySqlPluginIp != string.Empty)
                {
                    using (MySqlConnection restoreDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                                           server.MySqlPluginRoot + ";password=" +
                                                                           server.MySqlPluginPassword + ";"))
                    {

                        restoreDb.Open();
                        if (restoreDb.ServerVersion != null)
                        {
                            var sqlVer = Regex.Match(restoreDb.ServerVersion, @"\d+").Value;
                            if (Convert.ToInt32(sqlVer) < 8)
                            {
                                var cmd = restoreDb.CreateCommand();
                                var restoreDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "GRANT USAGE on *.* to '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL PRIVILEGES ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = restoreDbSql;
                                cmd.ExecuteNonQuery();
                                restoreDb.Close();
                            }
                            else
                            {
                                var cmd = restoreDb.CreateCommand();
                                var restoreDbSql = string.Concat(
                                    "CREATE DATABASE " + dbName + ";"
                                    , "CREATE USER '" + dbUser + "'@'%' IDENTIFIED BY " + "'" + dbPass + "';"
                                    , "GRANT ALL ON " + dbName + ".* to '" + dbUser + "'@'%';");
                                cmd.CommandText = restoreDbSql;
                                cmd.ExecuteNonQuery();
                                restoreDb.Close();
                            }
                        }

                        service.Variables["_MySQLPlugin::Host"] = server.MySqlPluginIp;
                        service.Variables["_MySQLPlugin::Username"] = dbUser;
                        service.Variables["_MySQLPlugin::Password"] = dbPass;
                        service.Variables["_MySQLPlugin::Database"] = dbName;
                    }

                    using (MySqlConnection restoreDb2 = new MySqlConnection(
                        "server=" + server.MySqlPluginIp + ";user=" +
                        server.MySqlPluginRoot + ";password=" +
                        server.MySqlPluginPassword + ";database=" + dbName + ";"))
                    {

                        var cmd2 = restoreDb2.CreateCommand();
                        var sqlRestore = new MySqlBackup(cmd2);
                        restoreDb2.Open();
                        sqlRestore.ImportFromFile(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, dbName + ".sql", server.OperatingSystem));
                        restoreDb2.Close();
                    }
                }
                
                System.IO.File.Delete(TCAdmin.SDK.Misc.FileSystem.CombinePath(service.RootDirectory, files[0].Name, server.OperatingSystem));

                service.Save();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        private static string GetDbUsername()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            return user.UserName + "_";
        }

        private static List<SelectListItem> GetDbDeletionUsernames()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            return (from service in services
                let usernameExists = service.Variables["_MySQLPlugin::Username"] != null
                where usernameExists
                let text = service.Variables["_MySQLPlugin::Database"].ToString()
                select new SelectListItem {Text = text, Value = service.ServiceId.ToString()}).ToList();
        }

        private static List<SelectListItem> GetDbResetUsernames()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            return (from service in services
                let usernameExists = service.Variables["_MySQLPlugin::Username"] != null
                where usernameExists
                let text = service.Variables["_MySQLPlugin::Database"].ToString()
                select new SelectListItem {Text = text, Value = service.ServiceId.ToString()}).ToList();
        }

        private static List<MySqlGridViewModel> GetUserDatabases()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                return (from service in services where service.Variables["_MySQLPlugin::Host"] != null let mysqlHost = service.Variables["_MySQLPlugin::Host"].ToString() let mysqlUser = service.Variables["_MySQLPlugin::Username"].ToString() let mysqlPass = service.Variables["_MySQLPlugin::Password"].ToString() let mysqlDatabase = service.Variables["_MySQLPlugin::Database"].ToString() let datacenter = new Datacenter(new Server(service.ServerId).DatacenterId) let server = new Server(service.ServerId) let phpmyAdmin = server.MySqlPluginUseDatacenter ? datacenter.MySqlPluginPhpMyAdmin : server.MySqlPluginPhpMyAdmin select new MySqlGridViewModel(mysqlHost, mysqlDatabase, mysqlUser, mysqlPass, datacenter.Location, phpmyAdmin, service.ServiceId.ToString())).ToList();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }
        }

        private static int GetOldUserDatabases()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            return services.Count(service => service.Variables.HasValue("MySQLUser") && !string.IsNullOrEmpty(service.Variables["MySQLUser"].ToString()));
        }

        private static int GetUserServicesCount()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false);

            return services.Count;
        }

        private static List<SelectListItem> GetUserServices()
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            return (from service in services
                    let usernameExists = service.Variables["_MySQLPlugin::Username"] != null
                    where !usernameExists
                    select new SelectListItem
                        {Text = service.ConnectionInfo + " - " + service.Name, Value = service.ServiceId.ToString()})
                .ToList();
        }

        private static MySqlEligibleLocations GetLocations()
        {
            var datacenters = new List<Datacenter>();
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var services = Service.GetServices(user, false)
                .Cast<Service>().ToList();

            try
            {
                ObjectBase.GlobalSkipSecurityCheck = true;
                foreach (var datacenter in services
                    .Select(service => new Datacenter(new Server(service.ServerId).DatacenterId)).Where(datacenter =>
                        datacenters.All(x => x.DatacenterId != datacenter.DatacenterId)))
                {
                    datacenters.Add(datacenter);
                }
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }

            return new MySqlEligibleLocations(datacenters);
        }
    }
}
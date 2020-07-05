using System.Collections.Generic;
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
                ResetUsernames = GetDbResetUsernames()
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
                var dbPass = System.Web.Security.Membership.GeneratePassword(12, 2);

                if (server.MySqlPluginUseDatacenter && datacenter.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var createDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                           datacenter.MySqlPluginRoot + ";password=" +
                                                           datacenter.MySqlPluginPassword + ";");
                        createDb.Open();
                        var cmd = createDb.CreateCommand();
                        var createDbSql = string.Concat(
                            "CREATE DATABASE " + dbName + ";"
                            , " GRANT USAGE on *.* to " + dbUser + "@`%` IDENTIFIED BY " + "'" + dbPass + "';"
                            , " GRANT ALL PRIVILEGES ON " + dbName + ".* to " + dbUser + "@`%`;");
                        cmd.CommandText = createDbSql;
                        cmd.ExecuteNonQuery();
                        createDb.Close();
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Host"] = datacenter.MySqlPluginIp;
                    service.Variables["_MySQLPlugin::Username"] = dbUser;
                    service.Variables["_MySQLPlugin::Password"] = dbPass;
                    service.Variables["_MySQLPlugin::Database"] = dbName;
                }
                else if (server.MySqlPluginUseDatacenter == false && server.MySqlPluginIp != string.Empty)
                {
                    try
                    {
                        var createDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                           server.MySqlPluginRoot + ";password=" +
                                                           server.MySqlPluginPassword +
                                                           ";");
                        createDb.Open();
                        var cmd = createDb.CreateCommand();
                        var createDbSql = string.Concat(
                            "CREATE DATABASE " + dbName + ";"
                            , " GRANT USAGE on *.* to " + dbUser + "@`%` IDENTIFIED BY " + "'" + dbPass + "';"
                            , " GRANT ALL PRIVILEGES ON " + dbName + ".* to " + dbUser + "@`%`;");
                        cmd.CommandText = createDbSql;
                        cmd.ExecuteNonQuery();
                        createDb.Close();
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Host"] = server.MySqlPluginIp;
                    service.Variables["_MySQLPlugin::Username"] = dbUser;
                    service.Variables["_MySQLPlugin::Password"] = dbPass;
                    service.Variables["_MySQLPlugin::Database"] = dbName;
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

                if (server.MySqlPluginUseDatacenter)
                {
                    try
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
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Host"] = null;
                    service.Variables["_MySQLPlugin::Username"] = null;
                    service.Variables["_MySQLPlugin::Password"] = null;
                    service.Variables["_MySQLPlugin::Database"] = null;
                }
                else
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
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Host"] = null;
                    service.Variables["_MySQLPlugin::Username"] = null;
                    service.Variables["_MySQLPlugin::Password"] = null;
                    service.Variables["_MySQLPlugin::Database"] = null;
                }

                service.Save();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }
            
            return JavaScript("window.location.reload(false);");
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

                if (server.MySqlPluginUseDatacenter)
                {
                    try
                    {
                        var resetDb = new MySqlConnection("server=" + datacenter.MySqlPluginIp + ";user=" +
                                                          datacenter.MySqlPluginRoot + ";password=" +
                                                          datacenter.MySqlPluginPassword + ";");
                        resetDb.Open();
                        var cmd = resetDb.CreateCommand();
                        var resetDbSql = "SET PASSWORD FOR '" + dbUser + "' = PASSWORD('" + dbPass + "');";
                        cmd.CommandText = resetDbSql;
                        cmd.ExecuteNonQuery();
                        resetDb.Close();
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Password"] = dbPass;
                }
                else
                {
                    try
                    {
                        var resetDb = new MySqlConnection("server=" + server.MySqlPluginIp + ";user=" +
                                                          server.MySqlPluginRoot + ";password=" +
                                                          server.MySqlPluginPassword + ";");
                        resetDb.Open();
                        var cmd = resetDb.CreateCommand();
                        var resetDbSql = "SET PASSWORD FOR '" + dbUser + "' = PASSWORD('" + dbPass + "');";
                        cmd.CommandText = resetDbSql;
                        cmd.ExecuteNonQuery();
                        resetDb.Close();
                    }
                    catch
                    {
                        return JavaScript(
                            "TCAdmin.Ajax.ShowBasicDialog('Error', 'Unable to connect to the remote MySQL host. Please contact an Administrator!');$('body').css('cursor', 'default');");
                    }

                    service.Variables["_MySQLPlugin::Password"] = dbPass;
                }

                service.Save();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }

            return JavaScript("window.location.reload(false);");
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
                return (from service in services
                    where service.Variables["_MySQLPlugin::Host"] != null
                    let mysqlHost = service.Variables["_MySQLPlugin::Host"].ToString()
                    let mysqlUser = service.Variables["_MySQLPlugin::Username"].ToString()
                    let mysqlPass = service.Variables["_MySQLPlugin::Password"].ToString()
                    let mysqlDatabase = service.Variables["_MySQLPlugin::Database"].ToString()
                    let datacenter = new Datacenter(new Server(service.ServerId).DatacenterId) 
                    select new MySqlGridViewModel(mysqlHost, mysqlDatabase, mysqlUser, mysqlPass, datacenter.Location,
                        datacenter.MySqlPluginPhpMyAdmin, service.ServiceId.ToString())).ToList();
            }
            finally
            {
                ObjectBase.GlobalSkipSecurityCheck = false;
            }
            
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
                        !datacenters.Any(x => x.DatacenterId == datacenter.DatacenterId)))
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
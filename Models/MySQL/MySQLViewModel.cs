using System.Collections.Generic;
using System.Web.Mvc;
using TCAdmin.SDK.Objects;
using TCAdmin.SDK.Web.MVC.Models;

namespace MySqlModule.Models.MySql
{
    public class MySqlModel
    {
        public int CurrentDatabases;
        public int MaxDatabases;
        public string CreationUsername;
        public int MigrationDbCount;
        public List<SelectListItem> CreationServiceIds;
        public List<SelectListItem> DbUsernames;
        public MySqlEligibleLocations EligibleLocations;
    }
    public class MySqlEligibleLocations : BaseModel
    {
        public MySqlEligibleLocations(List<Datacenter> datacenters)
        {
            Datacenters = datacenters;
        }

        public List<Datacenter> Datacenters { get; }
    }
    public class MySqlGridViewModel : BaseModel
    {
        public string DatabaseHost { get; }
        public string DatabaseName { get; }
        public string DatabaseUsername { get; }
        public string DatabasePassword { get; }
        public string DatabaseLocation { get; }
        public string DatabaseLink { get; }
        public string ServiceId { get; }
        
        public MySqlGridViewModel(string databaseHost, string databaseName, string databaseUsername, string databasePassword, string databaseLocation, string databaseLink, string serviceId)
        {
            DatabaseHost = databaseHost;
            DatabaseName = databaseName;
            DatabaseUsername = databaseUsername;
            DatabasePassword = databasePassword;
            DatabaseLocation = databaseLocation;
            DatabaseLink = databaseLink;
            ServiceId = serviceId;
        }
    }
}
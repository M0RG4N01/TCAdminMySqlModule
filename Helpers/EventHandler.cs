using MySqlModule.Controllers;
using TCAdmin.GameHosting.SDK.Objects;
using TCAdmin.SDK.Integration;

namespace MySqlModule.Helpers
{
    public class EventHandler : CommandBase
    {
        public override CommandResponse ProcessCommand(object sender, IntegrationEventArgs args)
        {

            var thisService = (Service) sender;
            var serviceEvent = (ServiceEvent) args.Arguments[0];

            if (thisService == null)
            {
                return new CommandResponse("d3b2aa93-7e2b-4e0d-8080-67d14b2fa8a9", ReturnStatus.SafeError);
            }

            switch (serviceEvent)
            {
                case ServiceEvent.BeforeDelete:
                case ServiceEvent.BeforeReinstall:
                    MySqlController.DeleteDatabase(thisService);
                    break;
                case ServiceEvent.BeforeMove:
                    MySqlController.BackupDatabaseOnMove(thisService, (TCAdmin.GameHosting.SDK.Automation.GameHostingMoveInfo)((System.Collections.Generic.Dictionary<string, object>)args.Arguments[1])["ThisGameHostingMoveInfo"]);
                    break;
                case ServiceEvent.AfterMove:
                    MySqlController.RestoreDatabaseOnMove(thisService, (TCAdmin.GameHosting.SDK.Automation.GameHostingMoveInfo)((System.Collections.Generic.Dictionary<string, object>)args.Arguments[1])["ThisGameHostingMoveInfo"]);
                    break;
            }

            return new CommandResponse("d3b2aa93-7e2b-4e0d-8080-67d14b2fa8a9",
                ReturnStatus.Ok);
        }
    }
}
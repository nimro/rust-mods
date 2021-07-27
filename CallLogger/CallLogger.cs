using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Call Logger", "nimro", "1.1.0")]
    [Description("Log call participants and times for admin review")]
    public class CallLogger : CovalencePlugin
    {
        private const string PERMISSION_VIEW_LOG = "calllogger.view";
        private const string PERMISSION_WIPE_LOG = "calllogger.wipe";
        private const string COMMAND = "gchq";
        private const string DATAFILE_NAME = "CallLoggerData";
        private const int MAX_VIEW_COUNT = 50;

        private CallLog _log;

        #region Commands
        [Command(COMMAND), Permission(PERMISSION_VIEW_LOG)]
        private void PrintLogToConsole(IPlayer player, string command, string[] args)
        {
            // Wipe command
            if (args.Length == 1 && args[0] == "wipe")
            {
                if (player.HasPermission(PERMISSION_WIPE_LOG))
                {
                    _log = new CallLog();
                    WriteDataFile();
                    player.Reply("The call log has been wiped.");
                    Log("The call log has been wiped.");
                }
                else
                {
                    player.Reply("You do not have permission to wipe the call log.");
                }
                return;
            }

            // View command
            var sb = new StringBuilder();
            sb.Append(string.Format("{0,-25} {1,-25} {2}\n", "Initiator", "Receiver", "Start Time"));
            sb.Append(string.Format("{0,-25} {1,-25} {2}\n", "---------", "--------", "----------"));
            foreach (LogEntry logEntry in _log.LogEntries.OrderByDescending(l => l.CallStartTime).Take(MAX_VIEW_COUNT))
            {
                sb.Append(string.Format("{0,-25} {1,-25} {2:o}\n",
                    logEntry.InitiatorName,
                    logEntry.ReceiverName,
                    logEntry.CallStartTime));
            }
            player.Reply(sb.ToString());
        }
        #endregion Commands

        #region Hooks
        void OnPhoneAnswered(PhoneController phone, PhoneController otherPhone, BasePlayer player)
        {
            var initiator = otherPhone.currentPlayer;
            var receiver = phone.currentPlayer;

            if (initiator == null || receiver == null || _log == null) { return; }

            _log.LogEntries.Add(new LogEntry
            {
                InitiatorID = initiator.userID,
                InitiatorName = initiator.displayName,
                ReceiverID = receiver.userID,
                ReceiverName = receiver.displayName,
                CallStartTime = DateTimeOffset.Now
            });

            WriteDataFile();
        }

        private void Loaded()
        {
            ReadDataFile();
            if (_log == null)
            {
                _log = new CallLog();
                WriteDataFile();
            }

            if (!permission.PermissionExists(PERMISSION_VIEW_LOG, this))
            {
                permission.RegisterPermission(PERMISSION_VIEW_LOG, this);
            }

            if (!permission.PermissionExists(PERMISSION_WIPE_LOG, this))
            {
                permission.RegisterPermission(PERMISSION_WIPE_LOG, this);
            }
        }

        private void Unload()
        {
            WriteDataFile();
        }
        #endregion Hooks

        #region Data
        private class CallLog
        {
            public HashSet<LogEntry> LogEntries { get; set; } = new HashSet<LogEntry>();
        }

        private class LogEntry
        {
            public string InitiatorName { get; set; }
            public ulong InitiatorID { get; set; }
            public string ReceiverName { get; set; }
            public ulong ReceiverID { get; set; }
            public DateTimeOffset CallStartTime { get; set; }
        }
        #endregion

        #region Helpers
        private void ReadDataFile()
        {
            _log = Interface.Oxide.DataFileSystem.ReadObject<CallLog>(DATAFILE_NAME);
        }

        private void WriteDataFile()
        {
            Interface.Oxide.DataFileSystem.WriteObject(DATAFILE_NAME, _log);
        }
        #endregion Helpers
    }
}

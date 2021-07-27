using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Call Logger", "nimro", "1.0.0")]
    [Description("Log call participants and times for admin review")]
    public class CallLogger : CovalencePlugin
    {
        private const string PERMISSION_VIEW_LOG = "calllogger.use";
        private const string COMMAND = "gchq";
        private const string DATAFILE_NAME = "CallLoggerData";
        private const int COMPARE_HISTORY_DAYS = 3;

        private CallLog _log;

        #region Commands
        [Command(COMMAND), Permission(PERMISSION_VIEW_LOG)]
        private void PrintLogToConsole(IPlayer player, string command, string[] args)
        {
            var compareDate = DateTimeOffset.Now.AddDays(COMPARE_HISTORY_DAYS * -1);
            var sb = new StringBuilder();
            sb.Append(string.Format("{0,-25} {1,-25}, {2}\n", "Initiator", "Receiver", "Start Time"));
            sb.Append(string.Format("{0,-25} {1,-25}, {2}\n", "_________", "________", "__________"));
            foreach (LogEntry logEntry in _log.LogEntries.Where(l => l.CallStartTime > compareDate).OrderByDescending(l => l.CallStartTime))
            {
                sb.Append(string.Format("{0,-25} {1,-25}, {2:o}\n",
                    logEntry.InitiatorName.Substring(0, 25),
                    logEntry.ReceiverName.Substring(0, 25),
                    logEntry.CallStartTime));
            }
            player.Reply(sb.ToString());
        }
        #endregion Commands

        #region Hooks
        object OnPhoneCallStart(PhoneController phone, PhoneController otherPhone, BasePlayer player)
        {
            var initiator = phone.currentPlayer;
            var receiver = otherPhone.currentPlayer;

            if (initiator is null || receiver is null || _log is null) { return null; }

            _log.LogEntries.Add(new LogEntry
            {
                InitiatorID = initiator.userID,
                InitiatorName = initiator.displayName,
                ReceiverID = initiator.userID,
                ReceiverName = initiator.displayName,
                CallStartTime = DateTimeOffset.Now
            });

            WriteDataFile();

            return null;
        }

        private void Init()
        {
            ReadDataFile();
            if (_log is null)
            {
                _log = new CallLog();
                WriteDataFile();
            }
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

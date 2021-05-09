using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Looter Spy", "nimro", "1.0.0")]
    [Description("Selectively monitor players looting containers to ensure they don't steal.")]
    public class LooterSpy : RustPlugin
    {
        private const string COMMAND = "lspy";
        private const string PERMISSION_ENABLE_LOOTERSPY = "looterspy.use";
        private static LooterSpy ins;
        private MonitorConfig monitoredPlayers;

        #region Commands
        [ChatCommand(COMMAND)]
        void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ENABLE_LOOTERSPY))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            if (args.Length != 2)
            {
                SendMessage(player, $"Usage: /{COMMAND} <enable | disable> <player steam id>");
                return;
            }
            else
            {
                ulong looterId;
                if (!ulong.TryParse(args[1], out looterId))
                {
                    SendMessage(player, $"Invalid player Id! Usage: /{COMMAND} <enable | disable> <player steam id>");
                    return;
                }

                LoadVariables();
                MonitorTuple requestedMonitor = new MonitorTuple(looterId, player.userID);
                if (args[0] == "enable")
                {
                    if (!monitoredPlayers.looterMonitors.Any(x => x.LooterID == looterId))
                    {
                        monitoredPlayers.looterMonitors.Add(requestedMonitor);
                        SaveConfig(monitoredPlayers);
                    }
                    Puts($"LooterSpy enabled for {looterId} by {player.displayName} ({player.userID})");
                    SendMessage(player, $"LooterSpy enabled for {looterId}");
                }
                else if (args[0] == "disable")
                {
                    if (monitoredPlayers.looterMonitors.Contains(requestedMonitor))
                    {
                        monitoredPlayers.looterMonitors = monitoredPlayers.looterMonitors.Where(lm => lm != requestedMonitor).ToList();
                        SaveConfig(monitoredPlayers);
                    }
                    Puts($"LooterSpy disabled for {looterId} by {player.displayName} ({player.userID})");
                    SendMessage(player, $"LooterSpy disabled for {looterId}");
                }
                else
                {
                    SendMessage(player, $"Invalid command! Usage: /{COMMAND} <enable | disable> <player steam id>");
                    return;
                }

                return;
            }
        }
        #endregion Commands

        #region Hooks
        void Loaded()
        {
            permission.RegisterPermission(PERMISSION_ENABLE_LOOTERSPY, this);
        }

        void OnServerInitialized()
        {
            ins = this;
            LoadDefaultConfig();
        }

        private void Unload()
        {
            ins = null;
        }

        void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid())
            {
                return;
            }

            LoadVariables();
            if (!monitoredPlayers.looterMonitors.Any(lm => lm.LooterID == looter.userID))
            {
                return;
            }

            var loot = entity.GetComponent<StorageContainer>();
            List<BasePlayer> moderators = monitoredPlayers.looterMonitors.Where(lm => lm.LooterID == looter.userID).Select(lm => BasePlayer.FindByID(lm.ModeratorID)).ToList();

            if (entity is BasePlayer)
            {
                BasePlayer lootee = entity.ToPlayer();
                moderators.ForEach(m => SendMessage(m, $"{looter.displayName} ({looter.userID}) started looting player {lootee.displayName} ({lootee.userID}).\nItems on body: \n{GetStorageItemsList(loot)}"));
            }
            else if (entity.OwnerID != 0ul && looter.userID != entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                moderators.ForEach(m => SendMessage(m, $"{looter.displayName} ({looter.userID}) started looting {entity.ShortPrefabName} belonging to {entity.OwnerID}.\nItems in container: \n{GetStorageItemsList(loot)}"));
            }
        }

        void OnLootEntityEnd(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid())
            {
                return;
            }

            LoadVariables();
            if (!monitoredPlayers.looterMonitors.Any(lm => lm.LooterID == looter.userID))
            {
                return;
            }

            var loot = entity.GetComponent<StorageContainer>();
            List<BasePlayer> moderators = monitoredPlayers.looterMonitors.Where(lm => lm.LooterID == looter.userID).Select(lm => BasePlayer.FindByID(lm.ModeratorID)).ToList();

            if (entity is BasePlayer)
            {
                BasePlayer lootee = entity.ToPlayer();
                moderators.ForEach(m => SendMessage(m, $"{looter.displayName} ({looter.userID}) finshed looting player {lootee.displayName} ({lootee.userID}).\nItems left on body: \n{GetStorageItemsList(loot)}"));
            }
            else if (entity.OwnerID != 0ul && looter.userID != entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                moderators.ForEach(m => SendMessage(m, $"{looter.displayName} ({looter.userID}) finished looting {entity.ShortPrefabName} belonging to {entity.OwnerID}.\nItems left in container: \n{GetStorageItemsList(loot)}"));
            }
        }
        #endregion Hooks

        #region Config
        private class MonitorTuple
        {
            public MonitorTuple() { }

            public MonitorTuple(ulong looterId, ulong moderatorId)
            {
                LooterID = looterId;
                ModeratorID = moderatorId;
            }

            public ulong LooterID { get; set; }
            public ulong ModeratorID { get; set; }

            private static bool MTEqualityCheck(MonitorTuple mt1, MonitorTuple mt2)
            {
                return mt1.LooterID == mt2.LooterID && mt1.ModeratorID == mt2.ModeratorID;
            }

            public override bool Equals(object obj)
            {
                if (obj is MonitorTuple)
                {
                    return MTEqualityCheck(this, (MonitorTuple)obj);
                }
                else
                {
                    return base.Equals(obj);
                }
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool operator ==(MonitorTuple mt1, MonitorTuple mt2)
            {
                return MTEqualityCheck(mt1, mt2);
            }

            public static bool operator !=(MonitorTuple mt1, MonitorTuple mt2)
            {
                return !MTEqualityCheck(mt1, mt2);
            }
        }
        private class MonitorConfig
        {
            public List<MonitorTuple> looterMonitors { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            MonitorConfig config = new MonitorConfig
            {
                looterMonitors = new List<MonitorTuple>()
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => monitoredPlayers = Config.ReadObject<MonitorConfig>();

        private void SaveConfig(MonitorConfig config) => Config.WriteObject(config, true);
        #endregion Config

        #region Helpers
        private string GetStorageItemsList(StorageContainer container)
        {
            StringBuilder sb = new StringBuilder();
            container.inventory.itemList
                .OrderBy(item => item.info.displayName.translated).ToList()
                .ForEach(item => sb.AppendLine($"Item: {item.info.displayName.translated} x{item.amount}"));
            return sb.ToString();
        }

        private bool IsAdmin(BasePlayer player) => player?.net?.connection?.authLevel > 0;

        private bool HasPermission(BasePlayer player, string permname) => IsAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0)
                    message = string.Format(message, args);
                SendReply(player, $"{message}");
            }
            else Puts(message);
        }
        #endregion Helpers
    }
}

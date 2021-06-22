using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Looter Spy", "nimro", "1.3.0")]
    [Description("Selectively monitor players looting containers to ensure they don't steal.")]
    public class LooterSpy : RustPlugin
    {
        private const string COMMAND = "lspy";
        private const string PERMISSION_ENABLE_LOOTERSPY = "looterspy.use";
        private static LooterSpy ins;
        private MonitorConfig config;

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
                    if (!config.looterMonitors.Any(x => x.LooterID == looterId))
                    {
                        config.looterMonitors.Add(requestedMonitor);
                        SaveConfig(config);
                    }
                    Puts($"LooterSpy enabled for {looterId} by {player.displayName} ({player.userID})");
                    SendMessage(player, $"LooterSpy enabled for {looterId}");
                }
                else if (args[0] == "disable")
                {
                    if (config.looterMonitors.Contains(requestedMonitor))
                    {
                        config.looterMonitors = config.looterMonitors.Where(lm => lm != requestedMonitor).ToList();
                        SaveConfig(config);
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
            if (!config.looterMonitors.Any(lm => lm.LooterID == looter.userID))
            {
                return;
            }

            if (entity is BasePlayer)
            {
                BasePlayer lootee = entity.ToPlayer();
                string items = "";
                int totalItems = 0;
                if (lootee.inventory.containerWear == null
                    || lootee.inventory.containerBelt == null
                    || lootee.inventory.containerMain == null)
                {
                    Puts("Looting player with null inventory, aborting");
                    return;
                }

                List<ItemContainer> containers = new List<ItemContainer>
                {
                    lootee.inventory.containerWear,
                    lootee.inventory.containerBelt,
                    lootee.inventory.containerMain
                };

                foreach (ItemContainer container in containers)
                {
                    items += GetStorageItemsList(container);
                    items += "\n";
                    totalItems += container.itemList.Select(i => i.amount).Sum();
                }
                GetWatchingModerators(looter.userID).ForEach(m => SendMessage(m,
                    $"{looter.displayName} ({looter.userID}) started looting player {lootee.displayName} ({lootee.userID})." +
                    $"\n{totalItems} Items on body: \n{items}"));
            }
            else if (entity.OwnerID != 0ul && looter.userID != entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                var loot = entity.GetComponent<StorageContainer>().inventory;
                BasePlayer owner = BasePlayer.FindByID(entity.OwnerID);
                string ownerinfo = "";
                if (owner != null)
                {
                    ownerinfo = $"{owner.displayName} ({entity.OwnerID})";
                }
                else
                {
                    ownerinfo = entity.OwnerID.ToString();
                }

                GetWatchingModerators(looter.userID).ForEach(m => SendMessage(m,
                    $"{looter.displayName} ({looter.userID}) started looting {entity.PrefabName} belonging to {ownerinfo} at {GetGrid(entity.transform.position, true)}." +
                    $"\n{loot.itemList.Select(i => i.amount).Sum()} Items in container: \n{GetStorageItemsList(loot)}"));
            }
        }

        void OnLootEntityEnd(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid())
            {
                return;
            }

            LoadVariables();
            if (!config.looterMonitors.Any(lm => lm.LooterID == looter.userID))
            {
                return;
            }

            if (entity is BasePlayer)
            {
                BasePlayer lootee = entity.ToPlayer();
                string items = "";
                int totalItems = 0;
                if (lootee.inventory.containerWear == null
                    || lootee.inventory.containerBelt == null
                    || lootee.inventory.containerMain == null)
                {
                    Puts("Looting player with null inventory, aborting");
                    return;
                }

                List<ItemContainer> containers = new List<ItemContainer>
                {
                    lootee.inventory.containerWear,
                    lootee.inventory.containerBelt,
                    lootee.inventory.containerMain
                };

                foreach (ItemContainer container in containers)
                {
                    items += GetStorageItemsList(container);
                    items += "\n";
                    totalItems += container.itemList.Select(i => i.amount).Sum();
                }
                GetWatchingModerators(looter.userID).ForEach(m => SendMessage(m,
                    $"{looter.displayName} ({looter.userID}) finished looting player {lootee.displayName} ({lootee.userID})." +
                    $"\n{totalItems} Items left on body: \n{items}"));
            }
            else if (entity.OwnerID != 0ul && looter.userID != entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                var loot = entity.GetComponent<StorageContainer>().inventory;
                BasePlayer owner = BasePlayer.FindByID(entity.OwnerID);
                string ownerinfo = "";
                if (owner != null)
                {
                    ownerinfo = $"{owner.displayName} ({entity.OwnerID})";
                }
                else
                {
                    ownerinfo = entity.OwnerID.ToString();
                }

                GetWatchingModerators(looter.userID).ForEach(m => SendMessage(m,
                    $"{looter.displayName} ({looter.userID}) finished looting {entity.PrefabName} belonging to {ownerinfo} at {GetGrid(entity.transform.position, true)}." +
                    $"\n{loot.itemList.Select(i => i.amount).Sum()} Items left in container: \n{GetStorageItemsList(loot)}"));
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
            public int OffsetYGrid { get; set; }
            public List<MonitorTuple> looterMonitors { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            MonitorConfig newconfig = new MonitorConfig
            {
                OffsetYGrid = -1,
                looterMonitors = new List<MonitorTuple>()
            };
            SaveConfig(newconfig);
        }

        private void LoadConfigVariables() => config = Config.ReadObject<MonitorConfig>();

        private void SaveConfig(MonitorConfig saveconfig) => Config.WriteObject(saveconfig, true);
        #endregion Config

        #region Helpers
        private string GetStorageItemsList(ItemContainer container)
        {
            StringBuilder sb = new StringBuilder();
            container.itemList
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
        }

        private List<BasePlayer> GetWatchingModerators(ulong looterUserId) =>
            config.looterMonitors
                .Where(lm => lm.LooterID == looterUserId)
                .Select(lm => BasePlayer.FindByID(lm.ModeratorID))
                .ToList();

        /// <summary>
        /// Slightly modified Grid(https://umod.org/plugins/grid) by yetzt where can can apply an offset to the grid number, as apparently its sometimes needed
        /// https://umod.org/community/grid/20658-grid-results-not-correct
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="addVector"></param>
        /// <returns></returns>
        string GetGrid(Vector3 pos, bool addVector)
        {

            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            var zoffset = z + config.OffsetYGrid;
            letter = (char)(((int)letter) + x);
            var grid = $"{letter}{zoffset}";
            if (addVector)
            {
                grid += $" {pos.ToString().Replace(",", "")}";
            }
            return grid;

        }
        #endregion Helpers
    }
}

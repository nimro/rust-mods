using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Looter Spy", "nimro", "2.0.2")]
    [Description("Selectively monitor players looting containers to ensure they don't steal.")]
    public class LooterSpy : CovalencePlugin
    {
        private const string COMMAND = "lspy";
        private const string PERMISSION_ENABLE_LOOTERSPY = "looterspy.use";
        private static LooterSpy ins;
        private MonitorConfig config;
        private Starts starts;

        #region Types
        private class Starts
        {
            public Hash<ulong, List<Item>> items = new Hash<ulong, List<Item>>();
        }
        #endregion Types

        #region Commands
        [Command(COMMAND), Permission(PERMISSION_ENABLE_LOOTERSPY)]
        void LooterSpyCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length != 2)
            {
                player.Reply($"Usage: /{COMMAND} <enable | disable> <player name or steam id>");
                return;
            }
            else
            {
                IPlayer looter = players.FindPlayer(args[1]);
                if (looter == null)
                {
                    player.Reply($"Player '{args[1]} not found.'");
                    return;
                }

                LoadVariables();
                MonitorTuple requestedMonitor = new MonitorTuple(looter.Id, player.Id);
                if (args[0] == "enable")
                {
                    if (!config.looterMonitors.Any(x => x.LooterID == looter.Id))
                    {
                        config.looterMonitors.Add(requestedMonitor);
                        SaveConfig(config);
                    }
                    Puts($"LooterSpy enabled for {looter.Name} ({looter.Id}) by {player.Name} ({player.Id})");
                    player.Reply($"LooterSpy enabled for {looter.Name} ({looter.Id})");
                }
                else if (args[0] == "disable")
                {
                    if (config.looterMonitors.Contains(requestedMonitor))
                    {
                        config.looterMonitors = config.looterMonitors.Where(lm => lm != requestedMonitor).ToList();
                        SaveConfig(config);
                    }
                    Puts($"LooterSpy disabled for {looter.Name} ({looter.Id}) by {player.Name} ({player.Id})");
                    player.Reply($"LooterSpy disabled for {looter.Name} ({looter.Id})");
                }
                else
                {
                    player.Reply($"Invalid command! Usage: /{COMMAND} <enable | disable> <player steam id>");
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
            starts = new Starts();
            LoadVariables();
        }

        void OnServerInitialized()
        {
            ins = this;
        }

        private void Unload()
        {
            ins = null;
            starts = null;
        }

        void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid())
            {
                return;
            }

            LoadVariables();
            if (!config.looterMonitors.Any(lm => lm.LooterID == looter.IPlayer.Id))
            {
                return;
            }

            if (entity is BasePlayer)
            {
                // OnLootEntityEnd isn't called for BasePlayers so don't record a start
                // I think this is an oxide bug
                return;
            }
            else if (entity.OwnerID == 0ul || looter.userID == entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                return;
            }

            RelationshipManager.PlayerTeam team;
            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(looter.userID, out team) && team.members.Contains(entity.OwnerID))
            {
                // Player is in a team with the owner, don't need to log this
                return;
            }

            starts.items.Remove(looter.userID); // make sure there's not already a started item in here
            starts.items.Add(looter.userID, ((IItemContainerEntity)entity).inventory.itemList.ToList());
        }

        void OnLootEntityEnd(BasePlayer looter, BaseCombatEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid())
            {
                return;
            }

            LoadVariables();
            if (!config.looterMonitors.Any(lm => lm.LooterID == looter.IPlayer.Id))
            {
                return;
            }

            if (entity is BasePlayer)
            {
                Puts("OnLootEntityEnd is working for BasePlayers now! Please tell nimro to update LooterSpy");
                return;
            }
            else if (entity.OwnerID != 0ul && looter.userID != entity.OwnerID) // don't report if owner is zero (world items) or if the player opens their own stuff
            {
                if (!starts.items.ContainsKey(looter.userID) || !(entity is IItemContainerEntity))
                {
                    return;
                }
                var loot = ((IItemContainerEntity)entity).inventory;

                var diff = GetAddedRemoved(starts.items[looter.userID], new List<ItemContainer>() { loot });
                var added = diff.Item1;
                var removed = diff.Item2;

                if (added.Count == 0 && removed.Count == 0)
                {
                    return;
                }

                IPlayer owner = players.FindPlayerById(entity.OwnerID.ToString());
                string ownerinfo = "";
                if (owner != null)
                {
                    ownerinfo = $"{owner.Name} ({entity.OwnerID})";
                }
                else
                {
                    ownerinfo = entity.OwnerID.ToString();
                }

                string message = $"{looter.displayName} ({looter.userID}) looted {entity.ShortPrefabName}\nbelonging to {ownerinfo}\nat {GetGrid(entity.transform.position, true)}.\n";
                if (added.Count > 0)
                {
                    message += $"\nADDED: \n{GetItemsList(added)}";
                }
                if (removed.Count > 0)
                {
                    message += $"\nREMOVED: \n{GetItemsList(removed)}";
                }
                GetWatchingModerators(looter.IPlayer.Id).ForEach(m => SendMessage(m, message));
            }
        }
        #endregion Hooks

        #region Config
        private class MonitorTuple
        {
            public MonitorTuple() { }

            public MonitorTuple(string looterId, string moderatorId)
            {
                LooterID = looterId;
                ModeratorID = moderatorId;
            }

            public string LooterID { get; set; }
            public string ModeratorID { get; set; }

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
            Puts("Loading default LooterSpy config");
            MonitorConfig newconfig = new MonitorConfig
            {
                OffsetYGrid = -1,
                looterMonitors = new List<MonitorTuple>()
            };
            SaveConfig(newconfig);
        }

        private void LoadConfigVariables()
        {
            try
            {
                var loadedConfig = Config.ReadObject<MonitorConfig>();
                if (loadedConfig == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    config = loadedConfig;
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        private void SaveConfig(MonitorConfig saveconfig) => Config.WriteObject(saveconfig, true);
        #endregion Config

        #region Helpers
        private string GetItemsList(List<ItemTotal> items)
        {
            var distinctItems = items.GroupBy(x => new { x.name, x.displayName }, x => x.count, (names, counts) => new ItemTotal(names.name, names.displayName, counts.Sum()));

            StringBuilder sb = new StringBuilder();
            distinctItems.OrderBy(item => item.displayName).ToList()
                         .ForEach(item => sb.AppendLine($"Item: {item.displayName} x{item.count}"));
            return sb.ToString();
        }

        private void SendMessage(IPlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0)
                {
                    message = string.Format(message, args);
                }
                player.Message($"{message}");
            }
        }

        private List<IPlayer> GetWatchingModerators(string looterUserId) =>
            config.looterMonitors
                .Where(lm => lm.LooterID == looterUserId)
                .Select(lm => players.FindPlayerById(lm.ModeratorID))
                .ToList();

        /// <summary>
        /// Slightly modified Grid(https://umod.org/plugins/grid) by yetzt where can can apply an offset to the grid number, as apparently its sometimes needed
        /// https://umod.org/community/grid/20658-grid-results-not-correct
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="addVector"></param>
        /// <returns></returns>
        private string GetGrid(Vector3 pos, bool addVector)
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

        private struct ItemTotal
        {
            public ItemTotal(string Name, string DisplayName, int Count)
            {
                name = Name;
                displayName = DisplayName;
                count = Count;
            }
            public string name { get; }
            public string displayName { get; }
            public int count { get; }
        }

        /// <summary>
        /// Calculate the difference between two containers
        /// </summary>
        /// <param name="start">Item containers present at the start</param>
        /// <param name="finish">Item containers present at the finish</param>
        /// <returns>Tuple where Item1 is the items added, and Item2 is the items removed</returns>
        private Tuple<List<ItemTotal>, List<ItemTotal>> GetAddedRemoved(List<Item> starting, List<ItemContainer> finish)
        {
            var added = new List<ItemTotal>();
            var removed = new List<ItemTotal>();

            List<Item> finishing = finish.SelectMany(f => f.itemList).ToList();

            foreach (Item item in starting)
            {
                var startCount = starting.Where(s => s.info.name == item.info.name).Select(s => s.amount).Sum();
                var finishCount = finishing.Where(f => f.info.name == item.info.name).Select(f => f.amount).Sum();

                if (finishCount > startCount)
                {
                    added.Add(new ItemTotal(item.info.name, item.info.displayName.translated, finishCount - startCount));
                }
                else if (finishCount < startCount)
                {
                    removed.Add(new ItemTotal(item.info.name, item.info.displayName.translated, startCount - finishCount));
                }
            }

            foreach (Item item in finishing.Where(f => !starting.Select(s => s.info.name).Contains(f.info.name)))
            {
                var finishCount = finishing.Where(f => f.info.name == item.info.name).Select(f => f.amount).Sum();
                added.Add(new ItemTotal(item.info.name, item.info.displayName.translated, finishCount));
            }

            return new Tuple<List<ItemTotal>, List<ItemTotal>>(added, removed);
        }
        #endregion Helpers
    }
}

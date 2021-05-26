using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("Zoned Crate Hack Timer", "nimro", "2.0.0")]
    [Description("Set custom timer reductions on hackable crates, by ZoneManager zone")]
    public class ZonedCrateHackTimer : RustPlugin
    {
        /*
         * This plugin works by reducing the default crate timer by the applicable amount
         * based on the zones the crate is in when the hacking begins.
         * 
         * To configure the plugin, create (or alter the default) config in the following format:
         *  {
         *      "IfConflictChoose": "lowest", // "lowest" or "highest"
         *      "ZoneTimerConfigs": [
         *           {
         *               "ZoneID": "a-zone",
         *               "TimerIncreaseSeconds": 123
         *           },
         *           {
         *               "ZoneID": "b-zone",
         *               "TimerIncreaseSeconds": 234
         *           }
         *      ]
         *  }
         */

        private ZoneTimerConfig config;
        private static ZonedCrateHackTimer ins;
        private const string CARGO_ZONE_ID = "cargo_ship";

        [PluginReference]
        private Plugin ZoneManager;

        #region Config
        private class ZoneTimerSetting
        {
            public string ZoneID { get; set; }
            public float TimerSeconds { get; set; }
        }

        private class ZoneTimerConfig
        {
            private string _IfConflictChoose;
            public string IfConflictChoose
            {
                get { return _IfConflictChoose ?? "lowest"; }
                set { _IfConflictChoose = value ?? "lowest"; }
            }
            public List<ZoneTimerSetting> ZoneTimerConfigs { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Puts($"Creating new empty {nameof(ZonedCrateHackTimer)} config");
            ZoneTimerConfig defaultConfig = new ZoneTimerConfig
            {
                IfConflictChoose = "lowest",
                ZoneTimerConfigs = new List<ZoneTimerSetting>()
                {
                    new ZoneTimerSetting()
                    {
                        ZoneID = "none",
                        TimerSeconds = 0
                    }
                }

            };
            SaveConfig(defaultConfig);
        }

        private void LoadConfigVariables() => config = Config.ReadObject<ZoneTimerConfig>();

        private void SaveConfig(ZoneTimerConfig config) => Config.WriteObject(config, true);
        #endregion Config

        #region Hooks
        void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null)
            {
                return;
            }

            float hackDiff = 0;
            List<string> crateZones = ZoneManager?.Call<string[]>("GetEntityZoneIDs", crate)?.ToList() ?? new List<string>();

            LoadVariables();

            var noneTimer = config.ZoneTimerConfigs
                .Where(ztc => ztc.ZoneID == "none")
                .DefaultIfEmpty(new ZoneTimerSetting() { ZoneID = "none", TimerSeconds = HackableLockedCrate.requiredHackSeconds })
                .FirstOrDefault();

            if (crate.GetParentEntity() != null)
            {
                if (crate.GetParentEntity() is CargoShip)
                {
                    Puts($"Crate parent is CargoShip, so adding '{CARGO_ZONE_ID}' to the list of zones");
                    crateZones.Add(CARGO_ZONE_ID);
                }
                else
                {
                    Puts($"Crate parent is '{crate.GetParentEntity()}'");
                }
            }

            if (crateZones == null || crateZones.Count == 0 || config == null)
            {
                Puts($"Hackable crate not in any zones, using 'none' timer of {noneTimer.TimerSeconds}");
                hackDiff = HackableLockedCrate.requiredHackSeconds - noneTimer.TimerSeconds;
                crate.hackSeconds = hackDiff;
                return;
            }

            List<ZoneTimerSetting> applicableConfigs = config.ZoneTimerConfigs.Where(cfg => crateZones.Contains(cfg.ZoneID)).ToList();

            if (applicableConfigs.Count == 0)
            {
                Puts($"No configured timer reduction for any of this hackable crate's zones ({string.Join(", ", crateZones)}). " +
                    $"Using 'none' timer of {noneTimer.TimerSeconds}");
                hackDiff = HackableLockedCrate.requiredHackSeconds - noneTimer.TimerSeconds;
                crate.hackSeconds = hackDiff;
                return;
            }

            ZoneTimerSetting mostApplicableConfig = config.IfConflictChoose == "highest"
                ? applicableConfigs.OrderByDescending(cfg => cfg.TimerSeconds).First()
                : applicableConfigs.OrderBy(cfg => cfg.TimerSeconds).First();
            float timeRemaining = mostApplicableConfig.TimerSeconds;
            timeRemaining = timeRemaining < 0 ? 0 : timeRemaining;

            Puts($"Hackable crate is in zone '{mostApplicableConfig.ZoneID}'. " +
                $"Changing timer from {HackableLockedCrate.requiredHackSeconds} to {timeRemaining}");
            // The underlying code unlocks the crate once hackSeconds > requiredHackSeconds
            hackDiff = HackableLockedCrate.requiredHackSeconds - mostApplicableConfig.TimerSeconds;
            crate.hackSeconds = hackDiff;
        }

        void OnServerInitialized()
        {
            ins = this;
            try
            {
                LoadConfigVariables();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                Puts($"{nameof(ZonedCrateHackTimer)} config was corrupt or malformed.");
                LoadDefaultConfig();
            }
        }

        private void Unload()
        {
            ins = null;
        }
        #endregion Hooks
    }
}

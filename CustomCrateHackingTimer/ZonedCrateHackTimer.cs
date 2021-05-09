using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("Zoned Crate Hack Timer", "nimro", "1.0.0")]
    [Description("Set custom timer reductions on hackable crates, by ZoneManager zone")]
    public class ZonedCrateHackTimer : RustPlugin
    {
        /*
         * This plugin works by reducing the default crate timer by the applicable amount
         * based on the zones the crate is in when the hacking begins.
         * 
         * To configure the plugin, create (or alter the default) config in the following format:
         *  {
         *      "ZoneTimerConfigs": [
         *           {
         *               "ZoneID": "a-zone",
         *               "TimerReductionSeconds": 123
         *           },
         *           {
         *               "ZoneID": "b-zone",
         *               "TimerReductionSeconds": 234
         *           }
         *      ]
         *  }
         */

        private ZoneTimerConfig config;
        private static ZonedCrateHackTimer ins;

        [PluginReference]
        private Plugin ZoneManager;

        #region Config
        private class ZoneTimerSetting
        {
            public string ZoneID { get; set; }
            public int TimerReductionSeconds { get; set; }
        }

        private class ZoneTimerConfig
        {
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
                ZoneTimerConfigs = new List<ZoneTimerSetting>()
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

            string[] crateZones = ZoneManager?.Call<string[]>("GetEntityZoneIDs", crate);
            LoadVariables();

            if (crateZones == null || crateZones.Length == 0 || config == null)
            {
                Puts($"Hackable crate not in any zones, using default timer of {HackableLockedCrate.requiredHackSeconds}");
                return;
            }

            List<ZoneTimerSetting> applicableConfigs = config.ZoneTimerConfigs.Where(cfg => crateZones.Contains(cfg.ZoneID)).ToList();

            if (applicableConfigs.Count == 0)
            {
                Puts($"No configured timer reduction for any of this hackable crate's zones ({string.Join(", ", crateZones)}). " +
                    $"Using default timer of {HackableLockedCrate.requiredHackSeconds}");
                return;
            }

            ZoneTimerSetting mostApplicableConfig = applicableConfigs.OrderByDescending(cfg => cfg.TimerReductionSeconds).First();
            float timeRemaining = HackableLockedCrate.requiredHackSeconds - mostApplicableConfig.TimerReductionSeconds;
            timeRemaining = timeRemaining < 0 ? 0 : timeRemaining;

            Puts($"Hackable crate is in zone '{mostApplicableConfig.ZoneID}'. " +
                $"Reducing timer from {HackableLockedCrate.requiredHackSeconds} to {timeRemaining}");
            // The underlying code unlocks the crate once hackSeconds > requiredHackSeconds
            crate.hackSeconds = mostApplicableConfig.TimerReductionSeconds;
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

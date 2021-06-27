using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/* MIT License
 * 
 * Copyright (c) 2021 nimro
 * 
 * Based on "Minicopter Lock" Copyright (c) 2021 Thisha
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace Oxide.Plugins
{
    [Info("Vehicle Lock", "nimro", "1.0.0")]
    [Description("Gives players the ability to lock vehicles")]
    class VehicleLock : RustPlugin
    {
        #region variables
        private const string keyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";
        private const string codeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string effectDenied = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string effectDeployed = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string keylockpermissionName = "vehiclelock.usekeylock";
        private const string codelockpermissionName = "vehiclelock.usecodelock";
        private const string kickpermissionName = "vehiclelock.kick";

        private const int doorkeyItemID = -1112793865;
        private const int keylockItemID = -850982208;
        private const int codelockItemID = 1159991980;

        private enum AllowedLockType { keylock, codelock, both };
        internal enum LockType { Keylock, Codelock, None };
        private enum PayType { Inventory, Resources, Free };

        private CooldownManager cooldownManager;
        #endregion variables

        #region localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission Key"] = "You are not allowed to add keylocks",
                ["No Permission Code"] = "You are not allowed to add codelocks",
                ["No Permission Kick"] = "You are not allowed to use the kick command",
                ["Cannot Afford"] = "You need a lock or the resources to craft one",
                ["Already Has Lock"] = "This minicopter already has a lock",
                ["Not A MiniCopter"] = "This entity is not a minicopter",
                ["Cooldown active"] = "You must wait approximately {0} seconds",
                ["Cannot have passengers"] = "Passengers must dismount first"
            }, this);
        }
        #endregion localization

        #region config
        private ConfigData config;

        class ConfigData
        {
            public bool SoundEffects { get; set; }

            public bool LocksAreFree { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            config = Config.ReadObject<ConfigData>();

            if (config == null)
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                SoundEffects = true,
                LocksAreFree = false
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion config

        #region chatommands
        [ChatCommand("lockit.key")]
        private void LockWithkeyLock2(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Keylock);
        }

        [ChatCommand("lockit.code")]
        private void LockWithCodeLock2(BasePlayer player, string command, string[] args)
        {
            LockIt(player, LockType.Codelock);
        }

        [ConsoleCommand("heli.kick")]
        private void KickPassenger(ConsoleSystem.Arg arg)
        {
            BasePlayer basePlayer = arg.Player();
            if (basePlayer == null)
                return;

            if (!permission.UserHasPermission(basePlayer.UserIDString, kickpermissionName))
            {
                basePlayer.ChatMessage(Lang("No Permission Kick", basePlayer.UserIDString));
                return;
            }

            if (basePlayer.isMounted)
            {
                BaseVehicle vehicle = basePlayer.GetMountedVehicle();
                MiniCopter miniCopter = vehicle.GetComponentInParent<MiniCopter>();
                if (miniCopter == null)
                    return;

                if (HasLock(miniCopter) == LockType.None)
                    return;

                if (basePlayer == vehicle.GetDriver())
                {
                    HasAnyAuthorizedMounted(miniCopter, basePlayer, true, true);
                }
            }
        }
        #endregion chatommands

        #region hooks
        private void Init()
        {
            permission.RegisterPermission(keylockpermissionName, this);
            permission.RegisterPermission(codelockpermissionName, this);
            permission.RegisterPermission(kickpermissionName, this);

            cooldownManager = new CooldownManager();
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            BaseVehicle vehicle = entity.GetComponentInParent<BaseVehicle>();
            if (vehicle == null)
                return null;

            BaseLock baseLock = vehicle.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return null;

            if (!baseLock.IsLocked())
                return null;

            if (!HasAnyAuthorizedMounted(vehicle, null, false, false))
            {
                if (PlayerIsAuthorized(player, vehicle))
                {
                    return null;
                }
                else
                {
                    if (config.SoundEffects)
                        Effect.server.Run(effectDenied, vehicle.transform.position);

                    return true;
                }
            }

            return null;
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            BaseVehicle vehicle = entity.GetComponentInParent<BaseVehicle>();
            if (vehicle == null)
                return;

            if (HasLock(vehicle) == LockType.None)
                return;

            if (PlayerIsAuthorized(player, vehicle))
                HasAnyAuthorizedMounted(vehicle, player, true, false);
        }

        object CanLock(BasePlayer player, KeyLock keyLock)
        {
            return CheckLock(player, keyLock, true);
        }

        object CanLock(BasePlayer player, CodeLock codeLock)
        {
            BaseVehicle vehicle = codeLock.GetComponentInParent<BaseVehicle>();
            if (vehicle == null)
                return null;

            if (vehicle.HasAnyPassengers() || vehicle.HasDriver())
                DismountPlayers(vehicle);

            return null;
        }

        object CanUnlock(BasePlayer player, KeyLock keyLock)
        {
            return CheckLock(player, keyLock, false);
        }

        object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            BaseVehicle vehicle = (codeLock.GetComponentInParent<BaseVehicle>());
            if (vehicle == null)
                return null;

            if (vehicle.HasAnyPassengers() || vehicle.HasDriver())
                DismountPlayers(vehicle);

            return null;
        }

        object CanPickupLock(BasePlayer player, BaseLock baseLock)
        {
            if (baseLock.GetComponentInParent<BaseVehicle>() != null)
                if (config.LocksAreFree)
                {
                    baseLock.Kill();
                    return false;
                }

            return null;
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            BaseVehicle vehicle = container.GetComponentInParent<BaseVehicle>();
            if (vehicle == null)
                return null;

            LockType lockType = HasLock(vehicle);
            if (lockType != LockType.None)
            {
                switch (lockType)
                {
                    case LockType.Keylock:
                        {
                            KeyLock keyLock = vehicle.GetComponentInChildren<KeyLock>();
                            if (!keyLock.IsLocked())
                                return null;

                            if (PlayerHasTheKey(player, Convert.ToInt32(vehicle.net.ID)))
                                return null;

                            break;
                        }

                    case LockType.Codelock:
                        {
                            CodeLock codeLock = vehicle.GetComponentInChildren<CodeLock>();
                            if (!codeLock.IsLocked())
                                return null;

                            if (codeLock.whitelistPlayers.Contains(player.userID))
                                return null;

                            break;
                        }
                }

                if (config.SoundEffects)
                    Effect.server.Run(effectDenied, vehicle.transform.position);

                return false;
            }

            return null;
        }

        object OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            MiniCopter miniCopter = (vehicle.GetComponentInParent<MiniCopter>());
            if (miniCopter == null)
                return null;

            BaseLock baseLock = miniCopter.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return null;

            if (!baseLock.IsLocked())
                return null;

            if (!PlayerIsAuthorized(player, miniCopter))
            {
                if (config.SoundEffects)
                    Effect.server.Run(effectDenied, miniCopter.transform.position);

                return vehicle;
            }

            return null;
        }
        #endregion hooks

        #region methods
        private void LockIt(BasePlayer player, LockType lockType)
        {
            switch (lockType)
            {
                case LockType.Keylock:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, keylockpermissionName))
                        {
                            player.ChatMessage(Lang("No Permission Key", player.UserIDString));
                            return;
                        }
                        break;
                    }

                case LockType.Codelock:
                    {
                        if (!permission.UserHasPermission(player.UserIDString, codelockpermissionName))
                        {
                            player.ChatMessage(Lang("No Permission Code", player.UserIDString));
                            return;
                        }
                        break;
                    }
            }

            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return;

            BaseEntity entity = hit.GetEntity();
            if (entity is MiniCopter)
            {
                MiniCopter miniCopter = entity.GetComponentInChildren<MiniCopter>();

                if ((miniCopter.HasAnyPassengers()) || (miniCopter.HasDriver()))
                {
                    player.ChatMessage(Lang("Cannot have passengers", player.UserIDString));
                    return;
                }

                if (HasLock(miniCopter) != LockType.None)
                {
                    player.ChatMessage(Lang("Already Has Lock", player.UserIDString));
                    return;
                }

                if (config.LocksAreFree == false)
                {
                    float secondsRemaining = 0;
                    if (PlayerHasCooldown(player.UserIDString, lockType, out secondsRemaining))
                    {
                        player.ChatMessage(Lang("Cooldown active", player.UserIDString, Math.Round(secondsRemaining).ToString()));
                        return;
                    }
                }

                PayType payType;
                if (CanAffordLock(player, lockType, out payType))
                {
                    if (lockType == LockType.Keylock)
                        AddKeylock(hit.GetEntity().GetComponent<MiniCopter>(), player);
                    else
                        AddCodelock(hit.GetEntity().GetComponent<MiniCopter>(), player, config.LocksAreFree);

                    PayForlock(player, lockType, payType);

                    cooldownManager.UpdateLastUsedForPlayer(player.UserIDString, lockType);
                }
                else
                    player.ChatMessage(Lang("Cannot Afford", player.UserIDString));
            }
            else if (entity is RidableHorse)
            {
                RidableHorse horse = entity.GetComponentInChildren<RidableHorse>();

                if ((horse.HasAnyPassengers()) || (horse.HasDriver()))
                {
                    player.ChatMessage(Lang("Cannot have passengers", player.UserIDString));
                    return;
                }

                if (HasLock(horse) != LockType.None)
                {
                    player.ChatMessage(Lang("Already Has Lock", player.UserIDString));
                    return;
                }

                if (config.LocksAreFree == false)
                {
                    float secondsRemaining = 0;
                    if (PlayerHasCooldown(player.UserIDString, lockType, out secondsRemaining))
                    {
                        player.ChatMessage(Lang("Cooldown active", player.UserIDString, Math.Round(secondsRemaining).ToString()));
                        return;
                    }
                }

                PayType payType;
                if (CanAffordLock(player, lockType, out payType))
                {
                    if (lockType == LockType.Keylock)
                        AddKeylock(hit.GetEntity().GetComponent<RidableHorse>(), player);
                    else
                        AddCodelock(hit.GetEntity().GetComponent<RidableHorse>(), player, config.LocksAreFree);

                    PayForlock(player, lockType, payType);

                    cooldownManager.UpdateLastUsedForPlayer(player.UserIDString, lockType);
                }
                else
                    player.ChatMessage(Lang("Cannot Afford", player.UserIDString));
            }
            else
            {
                player.ChatMessage(Lang("Not A MiniCopter", player.UserIDString));
            }
        }

        private LockType HasLock(BaseVehicle vehicle)
        {
            if (vehicle.GetComponentInChildren<KeyLock>())
                return LockType.Keylock;

            if (vehicle.GetComponentInChildren<CodeLock>())
                return LockType.Codelock;

            return LockType.None;
        }

        private bool CanAffordLock(BasePlayer player, LockType lockType, out PayType payType)
        {
            payType = PayType.Inventory;

            if (config.LocksAreFree)
            {
                payType = PayType.Free;
                return true;
            }

            int itemID = 0;

            switch (lockType)
            {
                case LockType.Keylock:
                    itemID = keylockItemID;
                    break;

                case LockType.Codelock:
                    itemID = codelockItemID;
                    break;
            }

            if ((uint)player.inventory.GetAmount(itemID) >= 1)
            {
                payType = PayType.Inventory;
                return true;
            }

            if (player.inventory.crafting.CanCraft(ItemManager.FindBlueprint(ItemManager.FindItemDefinition(itemID)), 1, false))
            {
                payType = PayType.Resources;
                return true;
            }

            return false;
        }

        private void PayForlock(BasePlayer player, LockType lockType, PayType payType)
        {
            if (payType == PayType.Free)
                return;

            int itemID = keylockItemID;
            if (lockType == LockType.Codelock)
                itemID = codelockItemID;

            if (payType == PayType.Inventory)
            {
                player.inventory.Take(new List<Item>(), itemID, 1);
            }
            else
            {
                List<Item> items = new List<Item>();
                foreach (ItemAmount ingredient in ItemManager.FindBlueprint(ItemManager.FindItemDefinition(itemID)).ingredients)
                {
                    player.inventory.Take(items, ingredient.itemid, (int)ingredient.amount);
                    player.Command("note.inv", new object[] { itemID, ((int)ingredient.amount * -1f) });
                }
            }
        }

        private void AddKeylock(BaseVehicle vehicle, BasePlayer player)
        {
            BaseEntity ent = GameManager.server.CreateEntity(keyLockPrefab, vehicle.transform.position);
            if (!ent)
                return;

            ent.Spawn();
            ent.SetParent(vehicle);

            if (vehicle is MiniCopter)
            {
                ent.transform.localEulerAngles = new Vector3(0, 180, 0);
                ent.transform.localPosition = new Vector3(0.27f, 0.67f, 0.1f);
            }
            else if (vehicle is ScrapTransportHelicopter)
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-1.31f, 1.28f, 1.74f);
            }
            else if (vehicle is RidableHorse)
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-0.32f, 1.25f, 0.1f);
            }

            KeyLock keylock = ent.GetComponent<KeyLock>();
            keylock.keyCode = Convert.ToInt32(vehicle.net.ID);
            keylock.OwnerID = player.userID;
            keylock.enableSaving = true;
            vehicle.SetSlot(BaseEntity.Slot.Lock, ent);

            ent.SendNetworkUpdateImmediate();
            if (config.SoundEffects)
                Effect.server.Run(effectDeployed, ent.transform.position);
        }

        private void AddCodelock(BaseVehicle vehicle, BasePlayer player, bool isfree)
        {
            BaseEntity ent = GameManager.server.CreateEntity(codeLockPrefab, vehicle.transform.position);
            if (!ent)
                return;

            ent.Spawn();
            ent.SetParent(vehicle);

            if (vehicle is MiniCopter)
            {
                ent.transform.localEulerAngles = new Vector3(0, 180, 0);
                ent.transform.localPosition = new Vector3(0.27f, 0.67f, 0.1f);
            }
            else if (vehicle is ScrapTransportHelicopter)
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-1.25f, 1.22f, 1.99f);
            }
            else if (vehicle is RidableHorse)
            {
                ent.transform.localEulerAngles = new Vector3(0, 0, 0);
                ent.transform.localPosition = new Vector3(-0.32f, 1.25f, 0.1f);
            }

            CodeLock codelock = ent.GetComponent<CodeLock>();
            codelock.OwnerID = 0;
            codelock.enableSaving = true;
            vehicle.SetSlot(BaseEntity.Slot.Lock, ent);

            ent.SendNetworkUpdateImmediate();
            if (config.SoundEffects)
                Effect.server.Run(effectDeployed, ent.transform.position);
        }

        private object CheckLock(BasePlayer player, KeyLock keyLock, bool forLocking)
        {
            BaseVehicle vehicle = (keyLock.GetComponentInParent<BaseVehicle>());
            if (vehicle == null)
                return null;

            if (forLocking)
            {
                if ((vehicle.HasAnyPassengers()) || (vehicle.HasDriver()))
                    DismountPlayers(vehicle);
            }

            if (PlayerHasTheKey(player, Convert.ToInt32(vehicle.net.ID)))
                return null;

            if (config.SoundEffects)
                Effect.server.Run(effectDenied, keyLock.transform.position);

            return false;
        }

        private bool HasAnyAuthorizedMounted(BaseVehicle vehicle, BasePlayer dismounted, bool kick, bool hardkick)
        {
            List<BaseVehicle.MountPointInfo>.Enumerator enumerator = vehicle.mountPoints.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    BaseVehicle.MountPointInfo current = enumerator.Current;
                    if (!(current.mountable != null))
                    {
                        continue;
                    }
                    else
                    {
                        BasePlayer player = current.mountable.GetMounted();
                        if (player == null)
                            continue;
                        else
                        {
                            if (player == dismounted)
                            {
                                continue;
                            }
                            else
                            {
                                if (hardkick)
                                {
                                    vehicle.GetComponent<BaseMountable>().DismountPlayer(player);
                                    player.EnsureDismounted();
                                    continue;
                                }

                                if (!PlayerIsAuthorized(player, vehicle))
                                {
                                    if (kick)
                                    {
                                        vehicle.GetComponent<BaseMountable>().DismountPlayer(player);
                                        player.EnsureDismounted();
                                    }
                                }
                                else
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }

        private bool PlayerIsAuthorized(BasePlayer player, BaseVehicle vehicle)
        {
            LockType lockType = HasLock(vehicle);

            switch (lockType)
            {
                case LockType.Keylock:
                    return (PlayerHasTheKey(player, Convert.ToInt32(vehicle.net.ID)));

                case LockType.Codelock:
                    return (vehicle.GetComponentInChildren<CodeLock>().whitelistPlayers.Contains(player.userID) || (vehicle.GetComponentInChildren<CodeLock>().guestPlayers.Contains(player.userID)));
            }

            return true;
        }

        private bool PlayerHasTheKey(BasePlayer player, int keyCode)
        {
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (IsMatchingKey(item, keyCode))
                    return true;
            }

            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (IsMatchingKey(item, keyCode))
                    return true;
            }

            return false;
        }

        private bool IsMatchingKey(Item item, int keyCode)
        {
            if (item.info.itemid == doorkeyItemID)
            {
                if (item.instanceData.dataInt == keyCode)
                    return true;
            }

            return false;
        }

        private void DismountPlayers(BaseVehicle vehicle)
        {
            List<BaseVehicle.MountPointInfo>.Enumerator enumerator = vehicle.mountPoints.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    BaseVehicle.MountPointInfo current = enumerator.Current;
                    if (!(current.mountable != null))
                    {
                        continue;
                    }
                    else
                    {
                        BasePlayer player = current.mountable.GetMounted();
                        if (player == null)
                            continue;
                        else
                        {
                            vehicle.GetComponent<BaseMountable>().DismountPlayer(player);
                            player.EnsureDismounted();
                        }
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }
        #endregion methods

        #region cooldown
        internal class CooldownManager
        {
            private readonly Dictionary<string, CooldownInfo> Cooldowns = new Dictionary<string, CooldownInfo>();

            public CooldownManager()
            {

            }

            private class CooldownInfo
            {
                public float CraftTime = Time.realtimeSinceStartup;
                public float CoolDown = 0;

                public CooldownInfo(float craftTime, float duration)
                {
                    CraftTime = craftTime;
                    CoolDown = duration;
                }
            }

            public void UpdateLastUsedForPlayer(string userID, LockType lockType)
            {
                string key = userID + "-" + lockType.ToString();
                float duration = 0;

                switch (lockType)
                {
                    case LockType.Keylock:
                        {
                            duration = ItemManager.FindBlueprint(ItemManager.FindItemDefinition(keylockItemID)).time;
                            break;
                        }

                    case LockType.Codelock:
                        {
                            duration = ItemManager.FindBlueprint(ItemManager.FindItemDefinition(codelockItemID)).time;
                            break;
                        }
                }

                if (Cooldowns.ContainsKey(key))
                {
                    Cooldowns[key].CraftTime = Time.realtimeSinceStartup;
                    Cooldowns[key].CoolDown = 10;
                }
                else
                {
                    CooldownInfo info = new CooldownInfo(Time.realtimeSinceStartup, duration);
                    Cooldowns.Add(key, info);
                }
            }

            public float GetSecondsRemaining(string userID, LockType lockType)
            {
                string key = userID + "-" + lockType.ToString();

                if (!Cooldowns.ContainsKey(key))
                    return 0;

                return Cooldowns[key].CraftTime + Cooldowns[key].CoolDown - Time.realtimeSinceStartup;
            }
        }

        private bool PlayerHasCooldown(string userID, LockType lockType, out float secondsRemaining)
        {
            secondsRemaining = (float)Math.Round(cooldownManager.GetSecondsRemaining(userID, lockType));

            if (secondsRemaining <= 0)
                return false;

            return true;
        }
        #endregion cooldown

        #region helpers
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        #endregion helpers
    }
}

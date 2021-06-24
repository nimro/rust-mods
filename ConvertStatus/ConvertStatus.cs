using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{

    [Info("Convert Status", "nimro", "2.0.0")]
    [Description("Change your admin status by a command")]
    public class ConvertStatus : RustPlugin
    {
        #region Vars

        [PluginReference]
        private Plugin Vanish;

        private const string perm_use = "convertstatus.use";
        private const string perm_mod = "convertstatus.mod";

        #endregion
    
        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(perm_use, this);
            permission.RegisterPermission(perm_mod, this);
            lang.RegisterMessages(messagesEN, this);
            lang.RegisterMessages(messagesRU, this, "ru");
        }

        #endregion

        #region Commands

        [ChatCommand("convert")]
        private void CmdConvert(BasePlayer p)
        {
            Convert(p);
        }

        #endregion

        #region Helpers

        private void Convert(BasePlayer p)
        {
            if (!HasPerm(p, perm_use) && !HasPerm(p, perm_mod))
            {
                message(p, "NOPERM");
                return;
            }

            if (p.Connection.authLevel != 0)
            {
                if (p.IsFlying)
                {
                    p.SendConsoleCommand("noclip");
                    message(p, "NOCLIP");
                    timer.Once(1f, () => Convert(p));
                    return;
                }

                ServerUsers.Set(p.userID, ServerUsers.UserGroup.None, "", "");
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                p.Connection.authLevel = 0;
                permission.RemoveUserGroup(p.UserIDString, "admin");
                permission.RemoveUserGroup(p.UserIDString, "supermod");
                Vanish.Call("Reappear", p);
            }
            else if (permission.UserHasPermission(p.UserIDString, perm_use))
            {
                ServerUsers.Set(p.userID, ServerUsers.UserGroup.Owner, "", "");
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                p.Connection.authLevel = 2;
                permission.AddUserGroup(p.UserIDString, "admin");
            }
            else if (permission.UserHasPermission(p.UserIDString, perm_mod))
            {
                ServerUsers.Set(p.userID, ServerUsers.UserGroup.Moderator, "", "");
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                p.Connection.authLevel = 1;
                permission.AddUserGroup(p.UserIDString, "supermod");
                Vanish.Call("Disappear", p);
                timer.Once(1f, () => p.SendConsoleCommand("noclip"));
            }

            var a = p.Connection.authLevel > 0 ? "into" : "out of";
            PrintWarning($"{p.displayName} converted {a} admin status");
            message(p, "CHANGED", p.Connection.authLevel > 0);
            ServerUsers.Save();
        }

        private bool HasPerm(BasePlayer p, string s)
        {
            return permission.UserHasPermission(p.UserIDString, s);
        }

        #endregion

        #region Language

        private Dictionary<string, string> messagesEN = new Dictionary<string, string>
        {
            {"NOPERM", "You don't have permission to that command!"},
            {"CHANGED", "Admin status now is <color=cyan>{0}</color>"},
            {"NOCLIP", "Fly will be deactivated in 1 sec. Don't use it in next 3 seconds or you will be banned!"},
        };
        
        private Dictionary<string, string> messagesRU = new Dictionary<string, string>
        {
            {"NOPERM", "У вас нет доступа к этой команде!"},
            {"CHANGED", "Ваш админ статус теперь <color=cyan>{0}</color>"},
            {"NOCLIP", "Режим полёта будет выключен через 1 секунду. Не используйте его в ближайшие 3 секуны или вы будете забанены!"},
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        #endregion
    }
}
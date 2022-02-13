using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Kit Groups", "WhiteThunder", "1.0.0")]
    [Description("Adds players to Oxide groups when they redeem Kits.")]
    internal class KitGroups : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Kits, TimedPermissions;

        private Configuration _pluginConfig;

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _pluginConfig.OnServerInitialized(this, TimedPermissions);
        }

        private void OnNewSave()
        {
            // Reset group membership
            foreach (var kitConfig in _pluginConfig.KitConfigs.Values)
            {
                var userIdList = permission.GetUsersInGroup(kitConfig.Group);
                foreach (var userId in userIdList)
                {
                    permission.RemoveUserGroup(userId, kitConfig.Group);
                }
            }
        }

        // This hook is exposed by plugin: Kits.
        private void OnKitRedeemed(BasePlayer player, string kitName)
        {
            if (player.IsNpc || !player.userID.IsSteamId())
            {
                return;
            }

            if (_pluginConfig.DebugLevel >= 2)
            {
                LogWarning($"Player {player.UserIDString} redeemed kit {kitName}");
            }

            var kitConfig = _pluginConfig.GetKitConfig(kitName);
            if (kitConfig == null)
            {
                if (_pluginConfig.DebugLevel >= 2)
                {
                    LogWarning($"Kit {kitName} has no KitGroups configuration.");
                }
                return;
            }

            if (kitConfig.DurationMinutes == 0)
            {
                if (_pluginConfig.DebugLevel >= 1)
                {
                    LogWarning($"Adding user {player.UserIDString} to group {kitConfig.Group} until next wipe.");
                }
                permission.AddUserGroup(player.UserIDString, kitConfig.Group);
                return;
            }

            if (_pluginConfig.DebugLevel >= 1)
            {
                LogWarning($"Adding user {player.UserIDString} to group {kitConfig.Group} for {kitConfig.DurationMinutes} minutes.");
            }

            AddToGroupTimed(player.UserIDString, kitConfig.Group, kitConfig.DurationMinutes);
        }

        #endregion

        #region Dependencies

        private bool KitExists(string kitName)
        {
            var result = Kits?.Call("IsKit", kitName);
            return result is bool && (bool)result;
        }

        private void AddToGroupTimed(string userId, string groupName, int durationMinutes)
        {
            if (TimedPermissions == null)
            {
                LogError($"Unable to add user {userId} to group {groupName} because TimedPermissions is not loaded.");
                return;
            }

            server.Command($"addgroup {userId} {groupName} {durationMinutes}m");
        }

        #endregion

        #region Configuration

        private class KitConfig
        {
            [JsonProperty("Group")]
            public string Group;

            [JsonProperty("Duration (minutes)")]
            public int DurationMinutes;
        }

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("DebugLevel")]
            public int DebugLevel;

            [JsonProperty("Kits")]
            public Dictionary<string, KitConfig> KitConfigs = new Dictionary<string, KitConfig>();

            public void OnServerInitialized(KitGroups pluginInstance, Plugin timedPermissions)
            {
                foreach (var entry in KitConfigs)
                {
                    var kitName = entry.Key;

                    if (!pluginInstance.KitExists(entry.Key))
                    {
                        pluginInstance.LogError($"Kit '{kitName}' does not exist.");
                        continue;
                    }

                    var kitConfig = entry.Value;
                    if (!pluginInstance.permission.GroupExists(kitConfig.Group))
                    {
                        pluginInstance.LogError($"Kit '{kitName}' specifies group '{kitConfig.Group}' which does not exist.");
                        continue;
                    }

                    if (kitConfig.DurationMinutes != 0 && timedPermissions == null)
                    {
                        pluginInstance.LogError($"Kit '{kitName}' has duration enabled, but TimedPermissions is not loaded.");
                        continue;
                    }
                }
            }

            public KitConfig GetKitConfig(string kitName)
            {
                KitConfig kitConfig;
                return KitConfigs.TryGetValue(kitName, out kitConfig)
                    ? kitConfig
                    : null;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Threading.Tasks;

using BoneLib.BoneMenu.Elements;

using LabFusion;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.SDK.Gamemodes;
using LabFusion.Senders;
using LabFusion.Utilities;
using MelonLoader;

namespace HideAndSeek
{
    internal class GamemodeClass : Gamemode
    {
        // Gamemode info
        public override string GamemodeCategory => "Hide and Seek";
        public override string GamemodeName => "Hide and Seek";

        // Gamemode settings
        public override bool AutoHolsterOnDeath => true;
        public override bool AutoStopOnSceneLoad => true;
        public override bool DisableDevTools => true;

        // Prefix
        public const string DefaultPrefix = "HideAndSeekMetadata";

        // Default metadata keys
        public const string PlayerSeekerKey = DefaultPrefix + ".Seeker";
        public const string PlayerFoundKey = DefaultPrefix + ".Found";
        public const string ForceOverrideAvatarsKey = DefaultPrefix + ".ForceOverrideAvatars";
        public const string HiderOverrideAvatarKey = DefaultPrefix + ".HiderOverrideAvatar";
        public const string SeekerOverrideAvatarKey = DefaultPrefix + ".SeekerOverrideAvatar";

        private bool _forceOverrideAvatars = true;

        private bool _isSeeker = false;
        private string _seekerOverrideAvatar = BoneLib.CommonBarcodes.Avatars.Tall;
        private string _hiderOverrideAvatar = BoneLib.CommonBarcodes.Avatars.Light;

        public override void OnGamemodeRegistered()
        {
            base.OnGamemodeRegistered();

            HookGamemodeEvents();
        }

        private void HookGamemodeEvents()
        {
            // Gamemode Events
            MultiplayerHooking.OnPlayerAction += OnPlayerAction;
        }

        public override void OnBoneMenuCreated(MenuCategory category)
        {
            base.OnBoneMenuCreated(category);

            category.CreateBoolElement("Force Override Avatars", UnityEngine.Color.white, _forceOverrideAvatars, (value) =>
            {
                _forceOverrideAvatars = value;
            });
        }

        protected override void OnMetadataChanged(string key, string value)
        {
            string[] splitKey = key.Split('.');

            switch (splitKey[1])
            {
                case "Found":
                    {
                        bool wasFound = bool.Parse(value);

                        // Return if the player wasn't found
                        if (!wasFound)
                            return;

                        // Get the player that was found
                        PlayerId foundPlayer = PlayerIdManager.PlayerIds.First(player => player.SmallId == byte.Parse(splitKey[2]));

                        // Return if the found player is the local player
                        if (foundPlayer == PlayerIdManager.LocalId)
                            return;

                        foundPlayer.TryGetDisplayName(out string displayName);

                        FusionNotifier.Send(new FusionNotification()
                        {
                            title = "Player Found!",
                            message = $"{displayName} was found!",
                            showTitleOnPopup = true,
                            popupLength = 3f,
                            type = NotificationType.WARNING
                        });

                        break;
                    }
                default:
                    {
                        MelonLogger.Error("Unknown metadata key: " + key);

                        break;
                    }
            }   
        }

        protected override void OnStartGamemode()
        {
            base.OnStartGamemode();

            SetupPlayerRoles(1);

            TryInvokeTrigger("OnGamemodeStarted");
        }

        protected override void OnStopGamemode()
        {
            base.OnStopGamemode();

            
        }

        private void SetGamemodeSettings()
        {
            TrySetMetadata(ForceOverrideAvatarsKey, _forceOverrideAvatars.ToString());
            TrySetMetadata(HiderOverrideAvatarKey, _hiderOverrideAvatar);
            TrySetMetadata(SeekerOverrideAvatarKey, _seekerOverrideAvatar);
        }

        /// <summary>
        /// Chooses random players based on the seeker count and returns a list of the seekers.
        /// </summary>
        /// <param name="seekerCount"></param>
        /// <returns></returns>
        private List<PlayerId> SetupPlayerRoles(byte seekerCount)
        {
            if (NetworkInfo.IsServer)
            {
                foreach (PlayerId player in PlayerIdManager.PlayerIds)
                {
                    TrySetMetadata(GetPlayerMetadataKey(player, PlayerSeekerKey), "false");
                }
            }

            // Choose random players based on the seekCount to be seekers whilst making sure not to choose the same player twice.
            List<PlayerId> seekers = new List<PlayerId>();
            for (int i = 0; i < seekerCount; i++)
            {
                PlayerId seeker = PlayerIdManager.PlayerIds[UnityEngine.Random.Range(0, PlayerIdManager.PlayerIds.Count)];
                while (seekers.Contains(seeker))
                {
                    seeker = PlayerIdManager.PlayerIds[UnityEngine.Random.Range(0, PlayerIdManager.PlayerIds.Count)];
                }
                seekers.Add(seeker);
                TrySetMetadata(GetPlayerMetadataKey(seeker, PlayerSeekerKey), "true");
            }

            return seekers;
        }
        
        private void OnPlayerAction(PlayerId playerId, PlayerActionType type, PlayerId otherPlayer)
        {
            throw new NotImplementedException();
        }

        protected override void OnEventTriggered(string value)
        {
            switch (value)
            {
                case "OnGamemodeStarted":
                    {
                        TryGetMetadata(GetPlayerMetadataKey(PlayerIdManager.LocalId, PlayerSeekerKey), out string isSeeker);
                        bool isSeekerBool = bool.Parse(isSeeker);

                        if (isSeekerBool)
                        {
                            foreach (PlayerId player in PlayerIdManager.PlayerIds)
                            {
                                if (player != PlayerIdManager.LocalId)
                                {
                                    SwipezGamemodeLib.Spectator.PlayerIdExtensions.Hide(player);
                                }
                            }
                        }

                        TryGetMetadata(ForceOverrideAvatarsKey, out string forceOverrideAvatars);
                        bool forceOverrideAvatarsBool = bool.Parse(forceOverrideAvatars);

                        if (forceOverrideAvatarsBool)
                        {
                            TryGetMetadata(isSeekerBool ? SeekerOverrideAvatarKey : HiderOverrideAvatarKey, out string avatarBarcode);

                            FusionPlayer.SetAvatarOverride(avatarBarcode);
                        }

                        FusionNotifier.Send(new FusionNotification()
                        {
                            title = "Round Begin!",
                            message = isSeekerBool ? "You are a seeker!" : "You are a hider!",
                            showTitleOnPopup = true,
                            popupLength = 3f,
                            type = NotificationType.INFORMATION
                        });

                        FusionPlayer.SetMortality(!isSeekerBool);

                        break;
                    }
                default:
                    {
                        MelonLogger.Error("Unknown event triggered: " + value);

                        break;
                    }
            }
        }

        private string GetPlayerMetadataKey(PlayerId playerId, string key)
        {
            return $"{key}.{playerId.SmallId}";
        }
    }
}

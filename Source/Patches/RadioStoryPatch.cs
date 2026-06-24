using System;
using System.Reflection;
using System.Text;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core;
using UBOAT.Game.Sandbox.Messages;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Stories;
using UBOAT.Game.UI;
using UnityEngine;

namespace EnigmaMod.Patches
{
    [HarmonyPatch(typeof(RadioStory))]
    internal static class RadioStoryPatch
    {
        private static string currentMessageId;
        private static RadioStory activeStory;
        private static bool updateListenerRegistered;
        private static int lastRevealed;

        private static readonly FieldInfo ContentsOverrideField = AccessTools.Field(typeof(RadioStory), "contentsOverride");
        private static readonly MethodInfo OnUpdatedMethod = AccessTools.Method(typeof(Story), "OnUpdated");
        private static readonly FieldInfo StoryPlayerField = AccessTools.Field(typeof(Story), "storyPlayerUI");
        private static readonly FieldInfo CharacterField = AccessTools.Field(typeof(RadioStory), "character");

        private const string FilledBlock = "\u2593";
        private const string EmptyBlock = "\u2591";

        [HarmonyPatch("GetFormattedMessageContents")]
        [HarmonyPostfix]
        private static void OnGetFormattedMessageContents(ref LocalizedString __result, IMessage message, RadioStory __instance)
        {
            PlayerShip playerShip = GetPlayerShip();
            if (message == null || __result == null || playerShip == null)
                return;

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            if (!shouldEncrypt)
                return;

            string rawText = __result;
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);

            currentMessageId = CreateMessageId(message);
            activeStory = __instance;

            if (!DecryptionRegistry.IsDecrypted(currentMessageId))
            {
                DecryptionRegistry.StartDecryption(currentMessageId, ciphertext.Length, ciphertext, rawText);

                int revealed = DecryptionRegistry.GetProgress(currentMessageId);
                lastRevealed = revealed;
                string radiomanName = GetRadiomanName(__instance);

                __result = LocalizedString.CreateUnlocalized(BuildDecryptionView(radiomanName, revealed, ciphertext.Length, rawText.Substring(0, Math.Min(revealed, rawText.Length))));

                RegisterUpdateListener(__instance);
            }
            else
            {
                string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
                if (plaintext != null)
                {
                    __result = LocalizedString.CreateUnlocalized(
                        $"\u2500\u2500 {Localization.GetDecryptedLabel()} \u2500\u2500\n{plaintext}"
                    );
                }
            }
        }

        [HarmonyPatch("OnStopped")]
        [HarmonyPostfix]
        private static void OnStopped(RadioStory __instance)
        {
            RemoveUpdateListener(__instance);
            activeStory = null;
            currentMessageId = null;
        }

        private static void UpdateDecryption()
        {
            if (activeStory == null || currentMessageId == null)
                return;

            IStoryPlayerUI storyPlayer = StoryPlayerField.GetValue(null) as IStoryPlayerUI;
            if (storyPlayer == null || !storyPlayer.IsOpened || storyPlayer.Story != activeStory)
                return;

            if (DecryptionRegistry.IsDecrypted(currentMessageId))
            {
                int revealed = DecryptionRegistry.GetProgress(currentMessageId);
                if (revealed != lastRevealed)
                {
                    lastRevealed = revealed;
                    ShowDecryptedAndCleanup();
                }
                return;
            }

            int progress = DecryptionRegistry.GetProgress(currentMessageId);
            if (progress != lastRevealed)
            {
                lastRevealed = progress;
                RefreshRadioDisplay();
            }
        }

        private static void ShowDecryptedAndCleanup()
        {
            if (activeStory == null || currentMessageId == null)
                return;

            string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
            var newContents = LocalizedString.CreateUnlocalized(
                $"\u2500\u2500 {Localization.GetDecryptedLabel()} \u2500\u2500\n{plaintext}"
            );
            ContentsOverrideField.SetValue(activeStory, newContents);
            OnUpdatedMethod.Invoke(activeStory, null);
            ShowNotification();
            RemoveUpdateListener(activeStory);
        }

        private static void RefreshRadioDisplay()
        {
            if (activeStory == null || currentMessageId == null)
                return;

            string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
            string ciphertext = DecryptionRegistry.GetCiphertext(currentMessageId);
            if (ciphertext == null)
                return;

            int totalChars = ciphertext.Length;
            int revealed = DecryptionRegistry.GetProgress(currentMessageId);
            string radiomanName = GetRadiomanName(activeStory);
            string revealedText = (plaintext ?? "").Substring(0, Math.Min(revealed, plaintext?.Length ?? 0));

            var newContents = LocalizedString.CreateUnlocalized(BuildDecryptionView(radiomanName, revealed, totalChars, revealedText));
            ContentsOverrideField.SetValue(activeStory, newContents);
            OnUpdatedMethod.Invoke(activeStory, null);
        }

        private static string BuildDecryptionView(string radiomanName, int revealed, int totalChars, string revealedText)
        {
            var sb = new StringBuilder();
            sb.Append($"\u2500\u2500 {radiomanName} \u2500\u2500\n");
            sb.Append(BuildProgressBar(revealed, totalChars));
            int percent = revealed * 100 / Math.Max(totalChars, 1);
            sb.Append($"  {percent}%\n");
            sb.Append(new string('\u2500', 28));
            sb.Append($"\n{Localization.GetDecryptingLabel()}: {revealed}/{totalChars}\n\n");
            sb.Append(revealedText);
            sb.Append("<color=#00ff00>\u2588</color>");
            return sb.ToString();
        }

        private static string BuildProgressBar(int filled, int total, int maxBlocks = 24)
        {
            int filledBlocks = Math.Min(filled * maxBlocks / Math.Max(total, 1), maxBlocks);
            int emptyBlocks = maxBlocks - filledBlocks;

            var sb = new StringBuilder();
            sb.Append("<color=#CC5500>");
            for (int i = 0; i < filledBlocks; i++)
                sb.Append(FilledBlock);
            sb.Append("</color>");
            sb.Append("<color=#1a0a00>");
            for (int i = 0; i < emptyBlocks; i++)
                sb.Append(EmptyBlock);
            sb.Append("</color>");
            return sb.ToString();
        }

        private static string GetRadiomanName(RadioStory story)
        {
            var character = CharacterField.GetValue(story) as PlayableCharacter;
            return character?.Name ?? "Radio Operator";
        }

        private static void RegisterUpdateListener(RadioStory story)
        {
            if (updateListenerRegistered)
                return;

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.AddUpdateListener(UpdateDecryption);
                updateListenerRegistered = true;
                Debug.Log("[EnigmaMod] RadioStory: registered decryption update listener");
            }
        }

        private static void RemoveUpdateListener(RadioStory story)
        {
            if (!updateListenerRegistered)
                return;

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.RemoveUpdateListener(UpdateDecryption);
                updateListenerRegistered = false;
                Debug.Log("[EnigmaMod] RadioStory: removed decryption update listener");
            }
        }

        private static void ShowNotification()
        {
            try
            {
                var notificationBar = InjectionFramework.Instance.GetInstance<INotificationBarUI>();
                if (notificationBar != null)
                {
                    notificationBar.OpenNow("", Localization.GetDecryptionCompleteMessage());
                    Debug.Log("[EnigmaMod] RadioStory: showed decryption complete notification");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[EnigmaMod] RadioStory: failed to show notification: " + e.Message);
            }
        }

        private static string CreateMessageId(IMessage message)
        {
            return message.SenderName + "|" + message.Date.Ticks;
        }

        private static PlayerShip GetPlayerShip()
        {
            return AccessTools.Field(typeof(Entity), "playerShip").GetValue(null) as PlayerShip;
        }

        private static bool ShouldEncrypt(IMessage message, PlayerShip playerShip)
        {
            if (message.Sender == playerShip.SandboxEntity)
                return false;
            if (message.Sender == null)
                return false;
            if (message.Sender.Country == null)
                return false;
            if (playerShip.Country == null)
                return false;
            return message.Sender.Country == playerShip.Country;
        }
    }
}

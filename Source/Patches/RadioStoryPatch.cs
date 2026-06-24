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
        private const string LogTag = "[EnigmaMod] RadioStoryPatch";

        [HarmonyPatch("GetFormattedMessageContents")]
        [HarmonyPostfix]
        private static void OnGetFormattedMessageContents(ref LocalizedString __result, IMessage message, RadioStory __instance)
        {
            PlayerShip playerShip = GetPlayerShip();
            if (message == null || __result == null || playerShip == null)
            {
                Debug.Log($"{LogTag}.GetFormattedMessageContents: SKIP — message={message != null}, __result={__result != null}, playerShip={playerShip != null}");
                return;
            }

            Debug.Log($"{LogTag}.GetFormattedMessageContents: senderName='{message.SenderName}', senderCountry={message.Sender?.Country?.CountryCode ?? "NULL"}, encMethod={message.EncryptionMethod}");

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            Debug.Log($"{LogTag}.GetFormattedMessageContents: shouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            string rawText = __result;
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);

            currentMessageId = CreateMessageId(message);
            activeStory = __instance;

            Debug.Log($"{LogTag}.GetFormattedMessageContents: rawLen={rawText.Length}, preprocessedLen={preprocessed.Length}, ciphertextLen={ciphertext.Length}, messageId='{currentMessageId}'");

            if (!DecryptionRegistry.IsDecrypted(currentMessageId))
            {
                DecryptionRegistry.StartDecryption(currentMessageId, ciphertext.Length, ciphertext, rawText);

                int revealed = DecryptionRegistry.GetProgress(currentMessageId);
                lastRevealed = revealed;
                string radiomanName = GetRadiomanName(__instance);

                Debug.Log($"{LogTag}.GetFormattedMessageContents: showing decryption view — revealed={revealed}/{ciphertext.Length}, radioman='{radiomanName}'");

                __result = LocalizedString.CreateUnlocalized(BuildDecryptionView(radiomanName, revealed, ciphertext.Length, rawText.Substring(0, Math.Min(revealed, rawText.Length))));

                RegisterUpdateListener(__instance);
            }
            else
            {
                string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
                Debug.Log($"{LogTag}.GetFormattedMessageContents: already decrypted — plaintextLen={plaintext?.Length ?? 0}");

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
            Debug.Log($"{LogTag}.OnStopped: cleaning up (messageId='{currentMessageId}')");
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
                    Debug.Log($"{LogTag}.UpdateDecryption: detected decryption completion (lastRevealed={lastRevealed} → {revealed})");
                    lastRevealed = revealed;
                    ShowDecryptedAndCleanup();
                }
                return;
            }

            int progress = DecryptionRegistry.GetProgress(currentMessageId);
            if (progress != lastRevealed)
            {
                Debug.Log($"{LogTag}.UpdateDecryption: progress changed {lastRevealed} → {progress}");
                lastRevealed = progress;
                RefreshRadioDisplay();
            }
        }

        private static void ShowDecryptedAndCleanup()
        {
            if (activeStory == null || currentMessageId == null)
            {
                Debug.LogWarning($"{LogTag}.ShowDecryptedAndCleanup: activeStory or currentMessageId is null");
                return;
            }

            string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
            Debug.Log($"{LogTag}.ShowDecryptedAndCleanup: showing DECRYPTED, plaintextLen={plaintext?.Length ?? 0}");

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
            {
                Debug.LogWarning($"{LogTag}.RefreshRadioDisplay: activeStory or currentMessageId is null");
                return;
            }

            string plaintext = DecryptionRegistry.GetPlaintext(currentMessageId);
            string ciphertext = DecryptionRegistry.GetCiphertext(currentMessageId);
            if (ciphertext == null)
            {
                Debug.LogWarning($"{LogTag}.RefreshRadioDisplay: ciphertext is null");
                return;
            }

            int totalChars = ciphertext.Length;
            int revealed = DecryptionRegistry.GetProgress(currentMessageId);
            string radiomanName = GetRadiomanName(activeStory);
            string revealedText = (plaintext ?? "").Substring(0, Math.Min(revealed, plaintext?.Length ?? 0));

            Debug.Log($"{LogTag}.RefreshRadioDisplay: updating display — revealed={revealed}/{totalChars}, radioman='{radiomanName}'");

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
            string name = character?.Name ?? "Radio Operator";
            Debug.Log($"{LogTag}.GetRadiomanName: character={(character != null ? character.Name : "null")} → '{name}'");
            return name;
        }

        private static void RegisterUpdateListener(RadioStory story)
        {
            if (updateListenerRegistered)
            {
                Debug.Log($"{LogTag}.RegisterUpdateListener: already registered");
                return;
            }

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.AddUpdateListener(UpdateDecryption);
                updateListenerRegistered = true;
                Debug.Log($"{LogTag}.RegisterUpdateListener: registered UpdateDecryption listener");
            }
            else
            {
                Debug.LogError($"{LogTag}.RegisterUpdateListener: ExecutionQueue is null!");
            }
        }

        private static void RemoveUpdateListener(RadioStory story)
        {
            if (!updateListenerRegistered)
            {
                Debug.Log($"{LogTag}.RemoveUpdateListener: not registered, nothing to remove");
                return;
            }

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.RemoveUpdateListener(UpdateDecryption);
                updateListenerRegistered = false;
                Debug.Log($"{LogTag}.RemoveUpdateListener: removed UpdateDecryption listener");
            }
            else
            {
                Debug.LogError($"{LogTag}.RemoveUpdateListener: ExecutionQueue is null!");
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
                    Debug.Log($"{LogTag}.ShowNotification: notification shown — '{Localization.GetDecryptionCompleteMessage()}'");
                }
                else
                {
                    Debug.LogWarning($"{LogTag}.ShowNotification: INotificationBarUI is null");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag}.ShowNotification: exception — {e.Message}");
            }
        }

        private static string CreateMessageId(IMessage message)
        {
            return message.SenderName + "|" + message.Date.Ticks;
        }

        private static PlayerShip GetPlayerShip()
        {
            var ship = AccessTools.Field(typeof(Entity), "playerShip").GetValue(null) as PlayerShip;
            Debug.Log($"{LogTag}.GetPlayerShip: {(ship != null ? $"found (country={ship.Country?.CountryCode ?? "null"})" : "null")}");
            return ship;
        }

        private static bool ShouldEncrypt(IMessage message, PlayerShip playerShip)
        {
            if (message.Sender == playerShip.SandboxEntity)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt: FALSE — outgoing message (sender is player ship)");
                return false;
            }

            if (message.Sender == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt: FALSE — message.Sender is null");
                return false;
            }

            if (message.Sender.Country == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt: FALSE — message.Sender.Country is null (senderName='{message.SenderName}')");
                return false;
            }

            if (playerShip.Country == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt: FALSE — playerShip.Country is null");
                return false;
            }

            bool result = message.Sender.Country == playerShip.Country;
            Debug.Log($"{LogTag}.ShouldEncrypt: {result} (senderCountry={message.Sender.Country.CountryCode}, playerCountry={playerShip.Country.CountryCode})");
            return result;
        }
    }
}

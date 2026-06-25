using System;
using System.Collections.Generic;
using System.Reflection;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Core;
using UBOAT.Game.Sandbox.Messages;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.UI.Journal;
using UnityEngine;

namespace EnigmaMod.Patches
{
    [HarmonyPatch(typeof(JournalMessageUI))]
    internal static class JournalMessageUIPatch
    {
        private static readonly FieldInfo MessageField = AccessTools.Field(typeof(JournalMessageUI), "message");
        private static readonly FieldInfo ContentsField = AccessTools.Field(typeof(JournalMessageUI), "contents");

        private static readonly Dictionary<JournalMessageUI, string> activeInstances = new Dictionary<JournalMessageUI, string>();
        private static bool updateListenerRegistered;

        private const string LogTag = "[EnigmaMod] JournalMessageUIPatch";

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void OnStart(JournalMessageUI __instance)
        {
            PlayerShip playerShip = PatchHelper.GetPlayerShip();
            IMessage message = MessageField.GetValue(__instance) as IMessage;
            if (message == null || playerShip == null)
            {
                Debug.Log($"{LogTag}.OnStart: SKIP — message={message != null}, playerShip={playerShip != null}");
                return;
            }

            TextMeshProUGUI contents = ContentsField.GetValue(__instance) as TextMeshProUGUI;
            if (contents == null)
            {
                Debug.Log($"{LogTag}.OnStart: SKIP — contents TMP field is null");
                return;
            }

            Debug.Log($"{LogTag}.OnStart: senderName='{message.SenderName}', senderCountry={message.Sender?.Country?.CountryCode ?? "NULL"}, encMethod={message.EncryptionMethod}");

            bool shouldEncrypt = PatchHelper.ShouldEncrypt(message, playerShip, "Journal");
            if (!shouldEncrypt)
                return;

            string rawText = message.GetFormattedContents();
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);
            string messageId = PatchHelper.CreateMessageId(message);

            Debug.Log($"{LogTag}.OnStart: rawLen={rawText.Length}, ciphertextLen={ciphertext.Length}, messageId='{messageId}'");

            DecryptionRegistry.Init();
            activeInstances[__instance] = messageId;

            if (!DecryptionRegistry.IsDecrypted(messageId))
            {
                DecryptionRegistry.StartDecryption(messageId, ciphertext.Length, ciphertext, rawText);

                int revealed = DecryptionRegistry.GetProgress(messageId);
                string label = Localization.GetCiphertextLabel();

                if (revealed <= 0)
                {
                    Debug.Log($"{LogTag}.OnStart: showing ENCRYPTED (no progress yet)");
                    contents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n<color=#888888>{Localization.GetUndecryptedLabel()}</color>";
                }
                else if (revealed >= ciphertext.Length)
                {
                    Debug.Log($"{LogTag}.OnStart: showing DECRYPTED (progress complete)");
                    contents.text = rawText;
                }
                else
                {
                    int percent = revealed * 100 / ciphertext.Length;
                    int plainRevealed = PatchHelper.GetRevealedPlaintextChars(messageId);
                    string revealedText = rawText.Substring(0, Math.Min(plainRevealed, rawText.Length));
                    string bar = PatchHelper.BuildProgressBar(revealed, ciphertext.Length);
                    string remaining = PatchHelper.FormatRemainingTime(DecryptionRegistry.GetEstimatedTimeRemaining(messageId));
                    string eta = string.IsNullOrEmpty(remaining) ? "" : $"\n<color=#888888>{remaining}</color>";
                    contents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n{bar}  {percent}%\n{Localization.GetDecryptingLabel()}: {revealed}/{ciphertext.Length}\n\n{revealedText}<color=#00ff00>|</color>{eta}";
                }

                RegisterUpdateListener();
            }
            else
            {
                string plaintext = DecryptionRegistry.GetPlaintext(messageId);
                Debug.Log($"{LogTag}.OnStart: already decrypted — plaintextLen={plaintext?.Length ?? 0}");
                if (plaintext != null)
                    contents.text = plaintext;
            }

            contents.rectTransform.sizeDelta = new Vector2(contents.rectTransform.sizeDelta.x, contents.preferredHeight);
        }

        private static void RegisterUpdateListener()
        {
            if (updateListenerRegistered)
                return;

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.AddUpdateListener(UpdateDecryption);
                updateListenerRegistered = true;
                Debug.Log($"{LogTag}.RegisterUpdateListener: registered");
            }
            else
            {
                Debug.LogError($"{LogTag}.RegisterUpdateListener: ExecutionQueue is null!");
            }
        }

        private static void RemoveUpdateListenerIfEmpty()
        {
            if (!updateListenerRegistered || activeInstances.Count > 0)
                return;

            var eq = ScriptableObjectSingleton.LoadSingleton<ExecutionQueue>();
            if (eq != null)
            {
                eq.RemoveUpdateListener(UpdateDecryption);
                updateListenerRegistered = false;
                Debug.Log($"{LogTag}.RemoveUpdateListenerIfEmpty: removed");
            }
        }

        private static void UpdateDecryption()
        {
            if (activeInstances.Count == 0)
                return;

            var toRemove = new List<JournalMessageUI>();

            foreach (var kv in activeInstances)
            {
                var instance = kv.Key;
                var messageId = kv.Value;

                if (instance == null)
                {
                    toRemove.Add(kv.Key);
                    continue;
                }

                if (DecryptionRegistry.IsDecrypted(messageId))
                {
                    string plaintext = DecryptionRegistry.GetPlaintext(messageId);
                    var contents = ContentsField.GetValue(instance) as TextMeshProUGUI;
                    if (contents != null && plaintext != null)
                    {
                        contents.text = plaintext;
                        contents.rectTransform.sizeDelta = new Vector2(contents.rectTransform.sizeDelta.x, contents.preferredHeight);
                    }
                    toRemove.Add(kv.Key);
                    continue;
                }

                int progress = DecryptionRegistry.GetProgress(messageId);
                if (progress <= 0)
                    continue;

                var state = DecryptionRegistry.GetState(messageId);
                if (state == null) continue;

                int percent = progress * 100 / Math.Max(state.TotalChars, 1);
                int plainRevealed = PatchHelper.GetRevealedPlaintextChars(messageId);
                string revealedText = (state.Plaintext ?? "").Substring(0, Math.Min(plainRevealed, state.Plaintext?.Length ?? 0));
                string bar = PatchHelper.BuildProgressBar(progress, state.TotalChars);
                string label = Localization.GetCiphertextLabel();

                string remaining = PatchHelper.FormatRemainingTime(DecryptionRegistry.GetEstimatedTimeRemaining(messageId));
                string eta = string.IsNullOrEmpty(remaining) ? "" : $"\n<color=#888888>{remaining}</color>";

                var tmpContents = ContentsField.GetValue(instance) as TextMeshProUGUI;
                if (tmpContents != null)
                {
                    tmpContents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{MessagePreprocessor.FormatCiphertext(state.Ciphertext)}</size></color>\n\n{bar}  {percent}%\n{Localization.GetDecryptingLabel()}: {progress}/{state.TotalChars}\n\n{revealedText}<color=#00ff00>|</color>{eta}";
                    tmpContents.rectTransform.sizeDelta = new Vector2(tmpContents.rectTransform.sizeDelta.x, tmpContents.preferredHeight);
                }
            }

            foreach (var key in toRemove)
                activeInstances.Remove(key);
            RemoveUpdateListenerIfEmpty();
        }
    }
}

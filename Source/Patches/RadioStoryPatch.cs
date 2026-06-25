using System;
using System.Collections.Generic;
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
        private class DecryptionContext
        {
            public string MessageId;
            public int LastRevealed;
        }

        private static readonly Dictionary<RadioStory, DecryptionContext> activeContexts = new Dictionary<RadioStory, DecryptionContext>();
        private static bool updateListenerRegistered;

        private static readonly FieldInfo ContentsOverrideField = AccessTools.Field(typeof(RadioStory), "contentsOverride");
        private static readonly MethodInfo OnUpdatedMethod = AccessTools.Method(typeof(Story), "OnUpdated");
        private static readonly FieldInfo StoryPlayerField = AccessTools.Field(typeof(Story), "storyPlayerUI");
        private static readonly FieldInfo CharacterField = AccessTools.Field(typeof(RadioStory), "character");

        private const string LogTag = "[EnigmaMod] RadioStoryPatch";

        [HarmonyPatch("GetFormattedMessageContents")]
        [HarmonyPostfix]
        private static void OnGetFormattedMessageContents(ref LocalizedString __result, IMessage message, RadioStory __instance)
        {
            PlayerShip playerShip = PatchHelper.GetPlayerShip();
            if (message == null || __result == null || playerShip == null)
            {
                Debug.Log($"{LogTag}.GetFormattedMessageContents: SKIP — message={message != null}, playerShip={playerShip != null}");
                return;
            }

            Debug.Log($"{LogTag}.GetFormattedMessageContents: senderName='{message.SenderName}', senderCountry={message.Sender?.Country?.CountryCode ?? "NULL"}, encMethod={message.EncryptionMethod}");

            bool shouldEncrypt = PatchHelper.ShouldEncrypt(message, playerShip, "RadioStory");
            Debug.Log($"{LogTag}.GetFormattedMessageContents: shouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            string rawText = __result;
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);

            string messageId = PatchHelper.CreateMessageId(message);
            var ctx = GetOrCreateContext(__instance, messageId);

            Debug.Log($"{LogTag}.GetFormattedMessageContents: rawLen={rawText?.Length ?? 0}, preprocessedLen={preprocessed?.Length ?? 0}, ciphertextLen={ciphertext?.Length ?? 0}, messageId='{messageId}'");

            if (!DecryptionRegistry.IsDecrypted(messageId))
            {
                DecryptionRegistry.StartDecryption(messageId, ciphertext.Length, ciphertext, rawText);

                int revealed = DecryptionRegistry.GetProgress(messageId);
                ctx.LastRevealed = revealed;
                string radiomanName = GetRadiomanName(__instance);

                Debug.Log($"{LogTag}.GetFormattedMessageContents: showing decryption view — revealed={revealed}/{ciphertext.Length}, radioman='{radiomanName}'");

                int plainRevealed = PatchHelper.GetRevealedPlaintextChars(messageId);
                string revealedText = (rawText ?? "").Substring(0, Math.Min(plainRevealed, rawText?.Length ?? 0));

                __result = LocalizedString.CreateUnlocalized(BuildDecryptionView(radiomanName, revealed, ciphertext.Length, revealedText));

                RegisterUpdateListener(__instance);
            }
            else
            {
                string plaintext = DecryptionRegistry.GetPlaintext(messageId);
                Debug.Log($"{LogTag}.GetFormattedMessageContents: already decrypted — plaintextLen={plaintext?.Length ?? 0}");

                if (plaintext != null)
                {
                    __result = LocalizedString.CreateUnlocalized(
                        $"==== {Localization.GetDecryptedLabel()} ====\n{plaintext}"
                    );
                }
            }
        }

        [HarmonyPatch("OnStopped")]
        [HarmonyPostfix]
        private static void OnStopped(RadioStory __instance)
        {
            Debug.Log($"{LogTag}.OnStopped: cleaning up context for instance {__instance.GetHashCode()}");
            RemoveUpdateListener(__instance);
            activeContexts.Remove(__instance);
        }

        private static DecryptionContext GetOrCreateContext(RadioStory story, string messageId)
        {
            if (activeContexts.TryGetValue(story, out var ctx))
            {
                ctx.MessageId = messageId;
                return ctx;
            }

            ctx = new DecryptionContext { MessageId = messageId, LastRevealed = -1 };
            activeContexts[story] = ctx;
            return ctx;
        }

        private static DecryptionContext GetContext(RadioStory story)
        {
            activeContexts.TryGetValue(story, out var ctx);
            return ctx;
        }

        private static void UpdateDecryption()
        {
            IStoryPlayerUI storyPlayer = StoryPlayerField.GetValue(null) as IStoryPlayerUI;
            if (storyPlayer == null || !storyPlayer.IsOpened)
                return;

            var activeStory = storyPlayer.Story as RadioStory;
            if (activeStory == null)
                return;

            var ctx = GetContext(activeStory);
            if (ctx == null || ctx.MessageId == null)
                return;

            if (DecryptionRegistry.IsDecrypted(ctx.MessageId))
            {
                int revealed = DecryptionRegistry.GetProgress(ctx.MessageId);
                if (revealed != ctx.LastRevealed)
                {
                    Debug.Log($"{LogTag}.UpdateDecryption: detected decryption completion (lastRevealed={ctx.LastRevealed} → {revealed})");
                    ctx.LastRevealed = revealed;
                    ShowDecryptedAndCleanup(activeStory, ctx);
                }
                return;
            }

            int progress = DecryptionRegistry.GetProgress(ctx.MessageId);
            if (progress != ctx.LastRevealed)
            {
                Debug.Log($"{LogTag}.UpdateDecryption: progress changed {ctx.LastRevealed} → {progress}");
                ctx.LastRevealed = progress;
                RefreshRadioDisplay(activeStory, ctx);
            }
        }

        private static void ShowDecryptedAndCleanup(RadioStory story, DecryptionContext ctx)
        {
            if (story == null || ctx.MessageId == null)
            {
                Debug.LogWarning($"{LogTag}.ShowDecryptedAndCleanup: story or messageId is null");
                return;
            }

            string plaintext = DecryptionRegistry.GetPlaintext(ctx.MessageId);
            Debug.Log($"{LogTag}.ShowDecryptedAndCleanup: showing DECRYPTED, plaintextLen={plaintext?.Length ?? 0}");

            var newContents = LocalizedString.CreateUnlocalized(
                $"==== {Localization.GetDecryptedLabel()} ====\n{plaintext}"
            );
            ContentsOverrideField.SetValue(story, newContents);
            OnUpdatedMethod.Invoke(story, null);
            PatchHelper.ShowNotification();
            RemoveUpdateListener(story);
        }

        private static void RefreshRadioDisplay(RadioStory story, DecryptionContext ctx)
        {
            if (story == null || ctx.MessageId == null)
            {
                Debug.LogWarning($"{LogTag}.RefreshRadioDisplay: story or messageId is null");
                return;
            }

            string plaintext = DecryptionRegistry.GetPlaintext(ctx.MessageId);
            string ciphertext = DecryptionRegistry.GetCiphertext(ctx.MessageId);
            if (ciphertext == null)
            {
                Debug.LogWarning($"{LogTag}.RefreshRadioDisplay: ciphertext is null");
                return;
            }

            int totalChars = ciphertext.Length;
            int revealed = DecryptionRegistry.GetProgress(ctx.MessageId);
            string radiomanName = GetRadiomanName(story);

            int plainRevealed = PatchHelper.GetRevealedPlaintextChars(ctx.MessageId);
            string revealedText = (plaintext ?? "").Substring(0, Math.Min(plainRevealed, plaintext?.Length ?? 0));

            Debug.Log($"{LogTag}.RefreshRadioDisplay: updating display — revealed={revealed}/{totalChars}, radioman='{radiomanName}'");

            var newContents = LocalizedString.CreateUnlocalized(BuildDecryptionView(radiomanName, revealed, totalChars, revealedText));
            ContentsOverrideField.SetValue(story, newContents);
            OnUpdatedMethod.Invoke(story, null);
        }

        private static string BuildDecryptionView(string radiomanName, int revealed, int totalChars, string revealedText)
        {
            var sb = new StringBuilder();
            sb.Append($"==== {radiomanName} ====\n");
            sb.Append(PatchHelper.BuildProgressBar(revealed, totalChars, 24));
            int percent = revealed * 100 / Math.Max(totalChars, 1);
            sb.Append($"  {percent}%\n");
            sb.Append(new string('=', 28));
            sb.Append($"\n{Localization.GetDecryptingLabel()}: {revealed}/{totalChars}\n\n");
            sb.Append(revealedText);
            sb.Append("<color=#00ff00>|</color>");
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

            bool hasActive = false;
            foreach (var kv in activeContexts)
            {
                if (kv.Key != null && kv.Key != story)
                {
                    hasActive = true;
                    break;
                }
            }

            if (hasActive)
            {
                Debug.Log($"{LogTag}.RemoveUpdateListener: other active stories still exist, keeping listener");
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
    }
}

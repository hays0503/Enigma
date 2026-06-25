using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
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
            Debug.Log($"{LogTag}.OnStart: shouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            string rawText = message.GetFormattedContents();
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);
            string messageId = PatchHelper.CreateMessageId(message);

            Debug.Log($"{LogTag}.OnStart: rawLen={rawText.Length}, ciphertextLen={ciphertext.Length}, messageId='{messageId}'");

            DecryptionRegistry.Init();

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
                    Debug.Log($"{LogTag}.OnStart: showing DECRYPTING — {revealed}/{ciphertext.Length} ({percent}%)");
                    contents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n{bar}  {percent}%\n{Localization.GetDecryptingLabel()}: {revealed}/{ciphertext.Length}\n\n{revealedText}<color=#00ff00>\u2588</color>";
                }
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
    }
}

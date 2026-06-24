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

        private const string FilledBlock = "\u2593";
        private const string EmptyBlock = "\u2591";
        private const string LogTag = "[EnigmaMod] JournalMessageUIPatch";

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void OnStart(JournalMessageUI __instance)
        {
            PlayerShip playerShip = GetPlayerShip();
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

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            Debug.Log($"{LogTag}.OnStart: shouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            string rawText = message.GetFormattedContents();
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);
            string messageId = message.SenderName + "|" + message.Date.Ticks;

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
                    string revealedText = rawText.Substring(0, revealed);
                    string bar = BuildProgressBar(revealed, ciphertext.Length);
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

        private static string BuildProgressBar(int filled, int total, int maxBlocks = 20)
        {
            int filledBlocks = filled * maxBlocks / total;
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
                Debug.Log($"{LogTag}.ShouldEncrypt: FALSE — outgoing message");
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

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

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void OnStart(JournalMessageUI __instance)
        {
            PlayerShip playerShip = GetPlayerShip();
            IMessage message = MessageField.GetValue(__instance) as IMessage;
            if (message == null || playerShip == null)
                return;

            TextMeshProUGUI contents = ContentsField.GetValue(__instance) as TextMeshProUGUI;
            if (contents == null)
                return;

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            if (!shouldEncrypt)
                return;

            string rawText = message.GetFormattedContents();
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);
            string messageId = message.SenderName + "|" + message.Date.Ticks;

            DecryptionRegistry.Init();

            if (!DecryptionRegistry.IsDecrypted(messageId))
            {
                DecryptionRegistry.StartDecryption(messageId, ciphertext.Length, ciphertext, rawText);

                int revealed = DecryptionRegistry.GetProgress(messageId);
                string label = Localization.GetCiphertextLabel();

                if (revealed <= 0)
                {
                    contents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n<color=#888888>{Localization.GetUndecryptedLabel()}</color>";
                }
                else if (revealed >= ciphertext.Length)
                {
                    contents.text = rawText;
                }
                else
                {
                    int percent = revealed * 100 / ciphertext.Length;
                    string revealedText = rawText.Substring(0, revealed);
                    string bar = BuildProgressBar(revealed, ciphertext.Length);
                    contents.text = $"<b>{label}</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n{bar}  {percent}%\n{Localization.GetDecryptingLabel()}: {revealed}/{ciphertext.Length}\n\n{revealedText}<color=#00ff00>\u2588</color>";
                }
            }
            else
            {
                string plaintext = DecryptionRegistry.GetPlaintext(messageId);
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

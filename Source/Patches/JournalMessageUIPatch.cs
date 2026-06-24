using System.Reflection;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Sandbox.Messages;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.UI;
using UBOAT.Game.UI.Journal;
using UnityEngine;

namespace EnigmaMod.Patches
{
    [HarmonyPatch(typeof(JournalMessageUI))]
    internal static class JournalMessageUIPatch
    {
        private static readonly FieldInfo MessageField = AccessTools.Field(typeof(JournalMessageUI), "message");
        private static readonly FieldInfo ContentsField = AccessTools.Field(typeof(JournalMessageUI), "contents");

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void OnStart(JournalMessageUI __instance)
        {
            PlayerShip playerShip = GetPlayerShip();

            IMessage message = MessageField.GetValue(__instance) as IMessage;
            if (message == null)
            {
                Debug.Log("[EnigmaMod] JournalMessageUI.Start: SKIP - message field is null");
                return;
            }

            if (playerShip == null)
            {
                Debug.Log("[EnigmaMod] JournalMessageUI.Start: SKIP - playerShip is null (not yet loaded?)");
                return;
            }

            TextMeshProUGUI contents = ContentsField.GetValue(__instance) as TextMeshProUGUI;
            Debug.Log($"[EnigmaMod] JournalMessageUI.Start: senderName='{message.SenderName}', senderEntity={message.Sender?.Name}, encMethod={message.EncryptionMethod}, isOutgoing={(message.Sender == playerShip.SandboxEntity)}, isRead={message.IsRead}");

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            Debug.Log($"[EnigmaMod] JournalMessageUI.Start: ShouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            if (contents == null)
            {
                Debug.Log("[EnigmaMod] JournalMessageUI.Start: SKIP - contents TMP field is null");
                return;
            }

            string plaintext = message.GetFormattedContents();
            string ciphertext = CaesarCipher.Encrypt(plaintext);

            Debug.Log($"[EnigmaMod] Encrypting message: plaintext='{plaintext.Substring(0, Mathf.Min(plaintext.Length, 100))}', ciphertext='{ciphertext.Substring(0, Mathf.Min(ciphertext.Length, 100))}'");

            contents.text = $"<color=#888888><size=75%>{ciphertext}</size></color>\n\n{plaintext}";
            contents.rectTransform.sizeDelta = new Vector2(contents.rectTransform.sizeDelta.x, contents.preferredHeight);

            var fitter = __instance.GetComponent<ChildrenSizeFitter>();
            if (fitter != null)
            {
                fitter.Fit();
            }
            else
            {
                Debug.Log("[EnigmaMod] JournalMessageUI.Start: ChildrenSizeFitter not found on instance");
            }
        }

        private static PlayerShip GetPlayerShip()
        {
            return AccessTools.Field(typeof(Entity), "playerShip").GetValue(null) as PlayerShip;
        }

        private static bool ShouldEncrypt(IMessage message, PlayerShip playerShip)
        {
            if (message.Sender == playerShip.SandboxEntity)
            {
                Debug.Log("[EnigmaMod] ShouldEncrypt: FALSE - message is outgoing (sender == player)");
                return false;
            }

            if (message.Sender == null)
            {
                Debug.Log("[EnigmaMod] ShouldEncrypt: FALSE - sender is null");
                return false;
            }

            if (message.Sender.Country == null)
            {
                Debug.Log($"[EnigmaMod] ShouldEncrypt: FALSE - sender.Country is null (sender={message.Sender.Name})");
                return false;
            }

            if (playerShip.Country == null)
            {
                Debug.Log("[EnigmaMod] ShouldEncrypt: FALSE - playerShip.Country is null");
                return false;
            }

            bool result = message.Sender.Country == playerShip.Country;
            Debug.Log($"[EnigmaMod] ShouldEncrypt: {result} (sender.Country={message.Sender.Country.CountryCode}, player.Country={playerShip.Country.CountryCode})");
            return result;
        }
    }
}

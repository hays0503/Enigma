using HarmonyLib;
using UBOAT.Game.Core;
using UBOAT.Game.Sandbox.Messages;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Stories;
using UnityEngine;

namespace EnigmaMod.Patches
{
    [HarmonyPatch(typeof(RadioStory))]
    internal static class RadioStoryPatch
    {
        [HarmonyPatch("GetFormattedMessageContents")]
        [HarmonyPostfix]
        private static void OnGetFormattedMessageContents(ref LocalizedString __result, IMessage message)
        {
            PlayerShip playerShip = GetPlayerShip();

            if (message == null)
            {
                Debug.Log("[EnigmaMod] RadioStory: SKIP - message is null");
                return;
            }

            Debug.Log($"[EnigmaMod] RadioStory: senderName='{message.SenderName}', encMethod={message.EncryptionMethod}, isOutgoing={(playerShip != null && message.Sender == playerShip.SandboxEntity)}");

            if (__result == null)
            {
                Debug.Log("[EnigmaMod] RadioStory: SKIP - result is null");
                return;
            }

            if (playerShip == null)
            {
                Debug.Log("[EnigmaMod] RadioStory: SKIP - playerShip is null (not yet loaded?)");
                return;
            }

            bool shouldEncrypt = ShouldEncrypt(message, playerShip);
            Debug.Log($"[EnigmaMod] RadioStory: ShouldEncrypt={shouldEncrypt}");

            if (!shouldEncrypt)
                return;

            string rawText = __result;
            string preprocessed = MessagePreprocessor.Preprocess(rawText);
            string ciphertext = CaesarCipher.Encrypt(preprocessed);
            string grouped = MessagePreprocessor.FormatCiphertext(ciphertext);

            Debug.Log($"[EnigmaMod] RadioStory encrypting: preprocessed='{preprocessed.Substring(0, Mathf.Min(preprocessed.Length, 80))}'");

            __result = LocalizedString.CreateUnlocalized(
                $"<b>Шифрограмма</b>\n<color=#888888><size=75%>{grouped}</size></color>\n\n{rawText}"
            );
        }

        private static PlayerShip GetPlayerShip()
        {
            return AccessTools.Field(typeof(Entity), "playerShip").GetValue(null) as PlayerShip;
        }

        private static bool ShouldEncrypt(IMessage message, PlayerShip playerShip)
        {
            if (message.Sender == playerShip.SandboxEntity)
            {
                Debug.Log("[EnigmaMod] RadioStory.ShouldEncrypt: FALSE - outgoing");
                return false;
            }

            if (message.Sender == null)
            {
                Debug.Log("[EnigmaMod] RadioStory.ShouldEncrypt: FALSE - sender null");
                return false;
            }

            if (message.Sender.Country == null)
            {
                Debug.Log($"[EnigmaMod] RadioStory.ShouldEncrypt: FALSE - sender.Country null ({message.Sender.Name})");
                return false;
            }

            if (playerShip.Country == null)
            {
                Debug.Log("[EnigmaMod] RadioStory.ShouldEncrypt: FALSE - playerShip.Country null");
                return false;
            }

            bool result = message.Sender.Country == playerShip.Country;
            Debug.Log($"[EnigmaMod] RadioStory.ShouldEncrypt: {result} (sender.Country={message.Sender.Country.CountryCode}, player.Country={playerShip.Country.CountryCode})");
            return result;
        }
    }
}

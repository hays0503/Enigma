using System;
using System.Reflection;
using System.Text;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Sandbox.Messages;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.UI;
using UnityEngine;

namespace EnigmaMod.Patches
{
    internal static class PatchHelper
    {
        private const string LogTag = "[EnigmaMod] PatchHelper";

        internal const string FilledBlock = "\u2593";
        internal const string EmptyBlock = "\u2591";

        internal static string BuildProgressBar(int filled, int total, int maxBlocks = 20)
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

        internal static PlayerShip GetPlayerShip()
        {
            var ship = AccessTools.Field(typeof(Entity), "playerShip").GetValue(null) as PlayerShip;
            Debug.Log($"{LogTag}.GetPlayerShip: {(ship != null ? $"found (country={ship.Country?.CountryCode ?? "null"})" : "null")}");
            return ship;
        }

        internal static bool ShouldEncrypt(IMessage message, PlayerShip playerShip, string source)
        {
            if (message.Sender == playerShip.SandboxEntity)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt({source}): FALSE — outgoing message");
                return false;
            }

            if (message.Sender == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt({source}): FALSE — message.Sender is null");
                return false;
            }

            if (message.Sender.Country == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt({source}): FALSE — message.Sender.Country is null (senderName='{message.SenderName}')");
                return false;
            }

            if (playerShip.Country == null)
            {
                Debug.Log($"{LogTag}.ShouldEncrypt({source}): FALSE — playerShip.Country is null");
                return false;
            }

            bool result = message.Sender.Country == playerShip.Country;
            Debug.Log($"{LogTag}.ShouldEncrypt({source}): {result} (senderCountry={message.Sender.Country.CountryCode}, playerCountry={playerShip.Country.CountryCode})");
            return result;
        }

        internal static int GetRevealedPlaintextChars(string messageId)
        {
            var state = DecryptionRegistry.GetState(messageId);
            if (state == null || state.TotalChars <= 0)
                return 0;

            int revealed = DecryptionRegistry.GetProgress(messageId);
            if (revealed <= 0)
                return 0;

            int plainLen = state.Plaintext?.Length ?? 0;
            int plainRevealed = plainLen * revealed / state.TotalChars;
            Debug.Log($"{LogTag}.GetRevealedPlaintextChars('{messageId}'): cipherRevealed={revealed}/{state.TotalChars}, plainLen={plainLen}, plainRevealed={plainRevealed}");
            return plainRevealed;
        }

        internal static void ShowNotification()
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

        internal static string CreateMessageId(IMessage message)
        {
            return message.SenderName + "|" + message.Date.Ticks;
        }
    }
}

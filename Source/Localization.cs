using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;
using UnityEngine;

namespace EnigmaMod
{
    public static class Localization
    {
        private static bool loggedLanguage;

        public static string GetCurrentLanguage()
        {
            var locale = ScriptableObjectSingleton.LoadSingleton<Locale>() as ILocale;
            string lang = locale?.Language?.ToLower() ?? "english";
            if (!loggedLanguage)
            {
                Debug.Log($"[EnigmaMod] Localization.GetCurrentLanguage: detected '{lang}' (locale={locale?.GetType()?.Name ?? "null"})");
                loggedLanguage = true;
            }
            return lang;
        }

        public static string GetCiphertextLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "CHIFFRAT";
                case "russian": return "ШИФРОГРАММА";
                default: return "CIPHERTEXT";
            }
        }

        public static string GetPressSpaceLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Drücken Sie LEERTASTE zum Entschlüsseln";
                case "russian": return "Нажмите ПРОБЕЛ для расшифровки";
                default: return "Press SPACE to decrypt";
            }
        }

        public static string GetDecryptingLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Entschlüssle";
                case "russian": return "Расшифровка";
                default: return "Decrypting";
            }
        }

        public static string GetDecryptedLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "ENTSCHLÜSSELT";
                case "russian": return "РАСШИФРОВАНО";
                default: return "DECRYPTED";
            }
        }

        public static string GetUndecryptedLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Nicht entschlüsselt";
                case "russian": return "Не расшифровано";
                default: return "Not decrypted";
            }
        }

        public static string GetDecryptionCompleteMessage()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Funknachricht entschlüsselt";
                case "russian": return "Радиограмма расшифрована";
                default: return "Radio message decoded";
            }
        }

        public static string GetPausedLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "[PAUSE]";
                case "russian": return "[ПАУЗА]";
                default: return "[PAUSED]";
            }
        }
    }
}

using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;

namespace EnigmaMod
{
    public static class Localization
    {
        public static string GetCurrentLanguage()
        {
            var locale = ScriptableObjectSingleton.LoadSingleton<Locale>() as ILocale;
            return locale?.Language?.ToLower() ?? "english";
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
                case "russian": return "Расшифровано";
                default: return "Decrypting";
            }
        }

        public static string GetDecryptedLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "ENTSCHLÜSSELT";
                case "russian": return "РАСШИФРОВАННО";
                default: return "DECRYPTED";
            }
        }

        public static string GetUndecryptedLabel()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Nicht entschlüsselt";
                case "russian": return "Не расшифрованно";
                default: return "Not decrypted";
            }
        }

        public static string GetDecryptionCompleteMessage()
        {
            switch (GetCurrentLanguage())
            {
                case "german": return "Funknachricht entschlüsselt";
                case "russian": return "Радиограмма расшифрованна";
                default: return "Radio message decoded";
            }
        }
    }
}

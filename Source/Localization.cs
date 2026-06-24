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
    }
}

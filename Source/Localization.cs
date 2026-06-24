using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;

namespace EnigmaMod
{
    public static class Localization
    {
        public static string GetCiphertextLabel()
        {
            var locale = ScriptableObjectSingleton.LoadSingleton<Locale>() as ILocale;
            if (locale == null)
                return "ШИФРОГРАММА";

            switch (locale.Language.ToLower())
            {
                case "english": return "CIPHERTEXT";
                case "german": return "CHIFFRAT";
                case "russian": return "ШИФРОГРАММА";
                default: return "CIPHERTEXT";
            }
        }
    }
}

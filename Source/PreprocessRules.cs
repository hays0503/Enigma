using System.Collections.Generic;

namespace EnigmaMod
{
    public class LanguageRules
    {
        public IReadOnlyDictionary<char, string> Punct { get; }
        public IReadOnlyDictionary<char, string> Digits { get; }
        public string Zero2 { get; }
        public string Zero3 { get; }
        public string Zero4 { get; }
        public bool ReplaceChWithQ { get; }

        public LanguageRules(
            Dictionary<char, string> punct,
            Dictionary<char, string> digits,
            string zero2,
            string zero3,
            string zero4,
            bool replaceChWithQ)
        {
            Punct = punct;
            Digits = digits;
            Zero2 = zero2;
            Zero3 = zero3;
            Zero4 = zero4;
            ReplaceChWithQ = replaceChWithQ;
        }
    }

    public static class PreprocessRules
    {
        public static LanguageRules ForLanguage(string lang)
        {
            switch (lang.ToLower())
            {
                case "german": return German;
                case "russian": return Russian;
                default: return English;
            }
        }

        private static readonly LanguageRules German = new LanguageRules(
            new Dictionary<char, string>
            {
                { '.', "X" },
                { ',', "Y" },
                { '?', "FRAGE" },
                { ':', "XX" },
                { '-', "YY" },
                { '\u2013', "YY" },
                { '\u2014', "YY" },
                { '/', "YY" },
                { '(', "KK" },
                { ')', "KK" },
            },
            new Dictionary<char, string>
            {
                { '0', "NULL" },
                { '1', "EINZ" },
                { '2', "ZWO" },
                { '3', "DREI" },
                { '4', "VIER" },
                { '5', "FUENF" },
                { '6', "SECHS" },
                { '8', "ACHT" },
                { '9', "NEUN" },
            },
            "CENTA",
            "MILLE",
            "MYRIA",
            true
        );

        private static readonly LanguageRules English = new LanguageRules(
            new Dictionary<char, string>
            {
                { '.', "STOP" },
                { ',', "COMMA" },
                { '?', "QUERY" },
                { ':', "COLON" },
                { '-', "DASH" },
                { '\u2013', "DASH" },
                { '\u2014', "DASH" },
                { '/', "DASH" },
                { '(', "PAREN" },
                { ')', "PAREN" },
            },
            new Dictionary<char, string>
            {
                { '0', "ZERO" },
                { '1', "ONE" },
                { '2', "TWO" },
                { '3', "THREE" },
                { '4', "FOUR" },
                { '5', "FIVE" },
                { '6', "SIX" },
                { '7', "SEVEN" },
                { '8', "EIGHT" },
                { '9', "NINE" },
            },
            "CENTA",
            "MILLE",
            "MYRIA",
            false
        );

        private static readonly LanguageRules Russian = new LanguageRules(
            new Dictionary<char, string>
            {
                { '.', "ТЧК" },
                { ',', "ЗПТ" },
                { '?', "ВПРС" },
                { ':', "ДВТЧ" },
                { '-', "ТИРЕ" },
                { '\u2013', "ТИРЕ" },
                { '\u2014', "ТИРЕ" },
                { '/', "ТИРЕ" },
                { '(', "СКБ" },
                { ')', "СКБ" },
            },
            new Dictionary<char, string>
            {
                { '0', "НУЛЬ" },
                { '1', "ОДИН" },
                { '2', "ДВА" },
                { '3', "ТРИ" },
                { '4', "ЧЕТЫРЕ" },
                { '5', "ПЯТЬ" },
                { '6', "ШЕСТЬ" },
                { '7', "СЕМЬ" },
                { '8', "ВОСЕМЬ" },
                { '9', "ДЕВЯТЬ" },
            },
            "ДВАНУЛЯ",
            "ТРИНУЛЯ",
            "ЧЕТЫРЕНУЛЯ",
            false
        );
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Time;
using UnityEngine;

namespace EnigmaMod
{
    [Serializable]
    public class DecryptionState
    {
        public string MessageId;
        public long StartTick;
        public int TotalChars;
        public bool IsDecrypted;
        public string Ciphertext;
        public string Plaintext;
    }

    [Serializable]
    public class DecryptionSaveData
    {
        public List<DecryptionState> States;
    }

    public static class DecryptionRegistry
    {
        private static readonly Dictionary<string, DecryptionState> states = new Dictionary<string, DecryptionState>();
        private static string savePath;
        private static bool initialized;
        private static GameTime gameTime;

        private const long TicksPerChar = 18000000000L;

        public static void Init()
        {
            if (initialized)
                return;
            initialized = true;
            savePath = Path.Combine(Application.persistentDataPath, "EnigmaMod", "decryption.json");
            gameTime = InjectionFramework.Instance.GetInstance<GameTime>();
            Load();
        }

        public static bool IsDecrypted(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var s))
                return false;
            if (s.IsDecrypted)
                return true;
            GetProgress(messageId);
            return s.IsDecrypted;
        }

        public static int GetProgress(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var state))
                return -1;
            if (state.IsDecrypted)
                return state.TotalChars;

            long elapsedTicks = gameTime.CurrentDateTime.Ticks - state.StartTick;
            int revealed = Math.Min(state.TotalChars, (int)(elapsedTicks / TicksPerChar));

            if (revealed >= state.TotalChars)
            {
                state.IsDecrypted = true;
                Save();
            }

            return revealed;
        }

        public static void StartDecryption(string messageId, int totalChars, string ciphertext, string plaintext)
        {
            Init();
            if (states.ContainsKey(messageId))
                return;

            states[messageId] = new DecryptionState
            {
                MessageId = messageId,
                StartTick = gameTime.CurrentDateTime.Ticks,
                TotalChars = totalChars,
                IsDecrypted = false,
                Ciphertext = ciphertext,
                Plaintext = plaintext
            };
            Save();
        }

        public static string GetCiphertext(string messageId)
        {
            Init();
            return states.TryGetValue(messageId, out var s) ? s.Ciphertext : null;
        }

        public static string GetPlaintext(string messageId)
        {
            Init();
            return states.TryGetValue(messageId, out var s) ? s.Plaintext : null;
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new DecryptionSaveData { States = new List<DecryptionState>(states.Values) };
                File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError("[EnigmaMod] Failed to save decryption registry: " + e.Message);
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(savePath))
                    return;

                var data = JsonUtility.FromJson<DecryptionSaveData>(File.ReadAllText(savePath));
                if (data?.States != null)
                {
                    states.Clear();
                    foreach (var state in data.States)
                        states[state.MessageId] = state;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[EnigmaMod] Failed to load decryption registry: " + e.Message);
            }
        }
    }
}

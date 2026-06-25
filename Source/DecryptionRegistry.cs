using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EnigmaMod
{
    [Serializable]
    public class DecryptionState
    {
        public string MessageId;
        public long StartTick;
        public int TotalChars;
        public int PlaintextLength;
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
        private const long TicksPerChar = 10000000L;
        private const string LogTag = "[EnigmaMod] DecryptionRegistry";

        public static void Init()
        {
            if (initialized)
                return;
            initialized = true;
            savePath = Path.Combine(Application.persistentDataPath, "EnigmaMod", "decryption.json");
            Debug.Log($"{LogTag}.Init: savePath='{savePath}'");

            Load();
        }

        public static bool IsDecrypted(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): false — no state found");
                return false;
            }
            if (s.IsDecrypted)
            {
                Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): true — already marked decrypted");
                return true;
            }
            GetProgress(messageId);
            Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): {s.IsDecrypted} (rechecked progress)");
            return s.IsDecrypted;
        }

        public static int GetProgress(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var state))
            {
                Debug.LogWarning($"{LogTag}.GetProgress('{messageId}'): -1 — no state found");
                return -1;
            }
            if (state.IsDecrypted)
            {
                Debug.Log($"{LogTag}.GetProgress('{messageId}'): already decrypted ({state.TotalChars}/{state.TotalChars})");
                return state.TotalChars;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            long elapsedTicks = nowTicks - state.StartTick;
            int revealed = Math.Min(state.TotalChars, (int)(elapsedTicks / TicksPerChar));

            Debug.Log($"{LogTag}.GetProgress('{messageId}'): nowTick={nowTicks}, startTick={state.StartTick}, elapsedTicks={elapsedTicks}, ticksPerChar={TicksPerChar}, revealed={revealed}/{state.TotalChars}");

            if (revealed >= state.TotalChars)
            {
                state.IsDecrypted = true;
                Debug.Log($"{LogTag}.GetProgress('{messageId}'): COMPLETE — marking as decrypted");
                Save();
            }

            return revealed;
        }

        public static void StartDecryption(string messageId, int totalChars, string ciphertext, string plaintext)
        {
            Init();
            if (states.ContainsKey(messageId))
            {
                Debug.Log($"{LogTag}.StartDecryption('{messageId}'): already exists, skipping (wasDecrypted={states[messageId].IsDecrypted})");
                return;
            }

            long startTick = DateTime.UtcNow.Ticks;
            Debug.Log($"{LogTag}.StartDecryption('{messageId}'): totalChars={totalChars}, startTick={startTick}");

            states[messageId] = new DecryptionState
            {
                MessageId = messageId,
                StartTick = startTick,
                TotalChars = totalChars,
                PlaintextLength = plaintext?.Length ?? 0,
                IsDecrypted = false,
                Ciphertext = ciphertext,
                Plaintext = plaintext
            };
            Save();
        }

        public static DecryptionState GetState(string messageId)
        {
            Init();
            states.TryGetValue(messageId, out var state);
            return state;
        }

        public static string GetCiphertext(string messageId)
        {
            Init();
            if (states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.GetCiphertext('{messageId}'): found, len={s.Ciphertext?.Length ?? 0}");
                return s.Ciphertext;
            }
            Debug.LogWarning($"{LogTag}.GetCiphertext('{messageId}'): not found, returning null");
            return null;
        }

        public static string GetPlaintext(string messageId)
        {
            Init();
            if (states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.GetPlaintext('{messageId}'): found, len={s.Plaintext?.Length ?? 0}");
                return s.Plaintext;
            }
            Debug.LogWarning($"{LogTag}.GetPlaintext('{messageId}'): not found, returning null");
            return null;
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Debug.Log($"{LogTag}.Save: created directory '{dir}'");
                }

                var data = new DecryptionSaveData { States = new List<DecryptionState>(states.Values) };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(savePath, json);
                Debug.Log($"{LogTag}.Save: saved {states.Count} states to '{savePath}' ({json.Length} bytes)");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag}.Save: FAILED — {e.Message}");
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(savePath))
                {
                    Debug.Log($"{LogTag}.Load: no save file at '{savePath}'");
                    return;
                }

                string json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<DecryptionSaveData>(json);
                if (data?.States != null)
                {
                    states.Clear();
                    foreach (var state in data.States)
                        states[state.MessageId] = state;
                    Debug.Log($"{LogTag}.Load: loaded {states.Count} states from '{savePath}' ({json.Length} bytes)");
                    foreach (var kv in states)
                        Debug.Log($"{LogTag}.Load:   '{kv.Key}': totalChars={kv.Value.TotalChars}, isDecrypted={kv.Value.IsDecrypted}, startTick={kv.Value.StartTick}");
                }
                else
                {
                    Debug.LogWarning($"{LogTag}.Load: save file exists but data is null or empty (jsonLen={json.Length})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag}.Load: FAILED — {e.Message}");
            }
        }
    }
}

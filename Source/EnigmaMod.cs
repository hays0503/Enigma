using System.Linq;
using System.Reflection;
using HarmonyLib;
using UBOAT.Game;
using UnityEngine;

namespace EnigmaMod
{
    public class EnigmaMod : IUserMod
    {
        public void OnLoaded()
        {
            Debug.Log("[EnigmaMod] ======= ENIGMA MOD LOADING =======");
            Debug.Log("[EnigmaMod] Version: 1.0.0");
            Debug.Log("[EnigmaMod] Assembly: " + GetType().Assembly.FullName);
            Debug.Log("[EnigmaMod] Game version: " + Application.unityVersion);
            Debug.Log("[EnigmaMod] Platform: " + Application.platform);

            Debug.Log("[EnigmaMod] Harmony patches discovery...");
            int count = 0;
            foreach (var type in GetType().Assembly.GetTypes())
            {
                var patches = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                if (patches.Length > 0)
                {
                    foreach (var attr in patches)
                    {
                        var patch = (HarmonyPatch)attr;
                        Debug.Log($"[EnigmaMod]   Found patch target: {patch.info.declaringType?.Name}.{patch.info.methodName}");
                    }
                    count++;
                }
            }
            Debug.Log($"[EnigmaMod] Found {count} patch classes");

            Harmony harmony = new Harmony("com.uboat.enigmamod");
            harmony.PatchAll();

            var patchedMethods = harmony.GetPatchedMethods().ToList();
            Debug.Log($"[EnigmaMod] Harmony.PatchAll() completed. Patched {patchedMethods.Count} methods:");
            foreach (var m in patchedMethods)
                Debug.Log($"[EnigmaMod]   Patched: {m.DeclaringType?.Name}.{m.Name}");

            Debug.Log("[EnigmaMod] ======= ENIGMA MOD LOADED =======");
        }
    }
}

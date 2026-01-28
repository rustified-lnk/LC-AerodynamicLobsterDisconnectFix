using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LobsterDisconnectFix;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[HarmonyPatch]
public class Plugin : BaseUnityPlugin {
    internal static new ManualLogSource Logger;

    internal static Harmony Harmony = new(MyPluginInfo.PLUGIN_GUID);

    internal static Sprite LobsterSprite;
    internal static AudioClip LobsterSound;

    private void Awake() {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        LobsterSprite = LoadSpriteFromEmbeddedResource("lobster.png");

        byte[] audioBytes = GetResourceBytes("lobster.wav");
        if (audioBytes != null) {
            LobsterSound = WavUtility.ToAudioClip(audioBytes, "LobsterMusic");
        }

        Harmony.PatchAll();
    }

    [HarmonyPatch(typeof(GameNetworkManager))]
    private static class GameNetworkManager_Patch {
        [HarmonyPatch("Singleton_OnClientDisconnectCallback")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void Singleton_OnClientDisconnectCallback_Postfix(GameNetworkManager __instance, ulong clientId) {
            if ((clientId == 0UL || clientId == NetworkManager.Singleton.LocalClientId) && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) {
                __instance.disconnectReason = 1;
                __instance.Disconnect();
            }
        }
    }

    //private static string GetFullPath(UnityEngine.Transform obj) { /// test
    //    string FullPath = "";
    //    while (obj != null) {
    //        FullPath = "\\" + obj.name + FullPath;
    //        obj = obj.parent;
    //    }
    //    return FullPath;
    //}

    private static byte[] GetResourceBytes(string resourceName, string firstName = "LobsterDisconnectFix.Embedded") {
        try {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(firstName + "." + resourceName)) {
                if (stream == null) {
                    Logger.LogError($"Resource not found: {resourceName}");
                    return null;
                }
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);
                return data;
            }
        } catch (Exception e) { Logger.LogError(e); return null; }
    }

    private static Sprite LoadSpriteFromEmbeddedResource(string resourceName, string firstName = "LobsterDisconnectFix.Embedded") {
        try {
            byte[] data = GetResourceBytes(resourceName, firstName);

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(data)) {
                texture.filterMode = FilterMode.Bilinear;
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        } catch (System.Exception e) {
            Logger.LogError($"Error loading sprite: {e.Message}");
        }
        return null;
    }

    [HarmonyPatch(typeof(MenuManager))]
    private static class DisplayMenuNotification_Patch {
        [HarmonyPatch("DisplayMenuNotification")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void DisplayMenuNotification_Postfix(MenuManager __instance, string notificationText, string buttonText) {
            //Logger.LogError(GetFullPath(__instance.menuNotification.transform));
            // get object by this path __instance.menuNotification -> Find Children by path /Panel
            // create image and write image texture by bytes in resources of plugin

            Transform panelTransform = __instance.menuNotification.transform.Find("Panel");

            Transform lobsterTransform = panelTransform.Find("LobsterImage");
            GameObject lobster = lobsterTransform != null ? lobsterTransform.gameObject : null;

            if (lobster == null) {
                if (LobsterSprite == null) {
                    Logger.LogWarning("Sprite not loaded, skipping lobster.");
                    return;
                }

                lobster = new("LobsterImage");

                lobster.transform.SetParent(panelTransform, false);

                Image imgComponent = lobster.AddComponent<Image>();
                imgComponent.sprite = LobsterSprite;
                imgComponent.preserveAspect = true;
                imgComponent.raycastTarget = false;

                LayoutElement le = lobster.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                RectTransform rect = lobster.GetComponent<RectTransform>();

                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);

                rect.pivot = new Vector2(0.5f, 0f);

                rect.anchoredPosition = new Vector2(0, 35);

                rect.sizeDelta = new Vector2(200, 200);

                //Mask mask = panelTransform.GetComponent<Mask>();
                //if (mask != null) mask.enabled = false;

                //RectMask2D rectMask = panelTransform.GetComponent<RectMask2D>();
                //if (rectMask != null) rectMask.enabled = false;

                //CanvasGroup cg = lobster.GetComponent<CanvasGroup>();
                //if (cg == null) cg = lobster.AddComponent<CanvasGroup>();
                //cg.blocksRaycasts = false;
                //cg.interactable = false;
                //cg.alpha = 1f;

                if (LobsterSound != null) {
                    AudioSource lobsterSource = lobster.GetComponent<AudioSource>();
                    if (lobsterSource == null) lobsterSource = lobster.AddComponent<AudioSource>();
                    lobsterSource.clip = LobsterSound;
                    lobsterSource.volume = 1f;
                    //lobsterSource.playOnAwake = false;
                }
            }

            var reason = GameNetworkManager.Instance.disconnectReason;

            //Logger.LogError("REASON: " + reason);

            if (reason == 1) {
                AudioSource lobsterSource = lobster.GetComponent<AudioSource>();
                if (lobsterSource != null)
                    //if (!lobsterSource.isPlaying)
                        lobsterSource.Play();

                lobster.SetActive(true);
            } else {
                lobster.SetActive(false);
            }


            //if (GameNetworkManager.Instance.disconnectReason == 1) {
            //lobster.SetActive(true);
        }
    }
}

public static class WavUtility {
    public static AudioClip ToAudioClip(byte[] wavFile, string name = "wav") {
        int subchunk1Size = BitConverter.ToInt32(wavFile, 16);
        int audioFormat = BitConverter.ToInt16(wavFile, 20);
        int channels = BitConverter.ToInt16(wavFile, 22);
        int frequency = BitConverter.ToInt32(wavFile, 24);
        int bitDepth = BitConverter.ToInt16(wavFile, 34);

        int pos = 12 + 8 + subchunk1Size;
        while (pos < wavFile.Length) {
            if (wavFile[pos] == 0x64 && wavFile[pos + 1] == 0x61 && wavFile[pos + 2] == 0x74 && wavFile[pos + 3] == 0x61) {
                pos += 4;
                break;
            }
            pos++;
        }
        int dataSize = BitConverter.ToInt32(wavFile, pos);
        pos += 4;

        float[] data = new float[dataSize / (bitDepth / 8)];
        int offset = 0;

        if (bitDepth == 16) {
            for (int i = 0; i < data.Length; i++) {
                short value = BitConverter.ToInt16(wavFile, pos + offset);
                data[i] = value / 32768f;
                offset += 2;
            }
        }
        else if (bitDepth == 8) {
            for (int i = 0; i < data.Length; i++) {
                data[i] = (wavFile[pos + offset] - 128) / 128f;
                offset += 1;
            }
        }
        else if (bitDepth == 32) {
            for (int i = 0; i < data.Length; i++) {
                data[i] = BitConverter.ToSingle(wavFile, pos + offset);
                offset += 4;
            }
        }

        AudioClip clip = AudioClip.Create(name, data.Length / channels, channels, frequency, false);
        clip.SetData(data, 0);
        return clip;
    }
}
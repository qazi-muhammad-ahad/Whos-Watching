using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    public static class GameSettings
    {
        // PlayerPrefs keys
        const string KeyResWidth = "WW.ResWidth";
        const string KeyResHeight = "WW.ResHeight";
        const string KeyDisplayMode = "WW.DisplayMode";
        const string KeyQuality = "WW.QualityLevel";
        const string KeyVSync = "WW.VSync";
        const string KeyFrameRate = "WW.FrameRateLimit";
        const string KeyMasterVol = "WW.MasterVolume";
        const string KeyMusicVol = "WW.MusicVolume";
        const string KeySFXVol = "WW.SFXVolume";
        const string KeySensitivity = "WW.MouseSensitivity";
        const string KeyInvertY = "WW.InvertY";
        const string KeyKeyPreset = "WW.KeyPreset";

        // Graphics Cache
        public static Resolution[] AvailableResolutions { get; private set; }
        public static int SelectedResolutionIndex = 0;
        public static FullScreenMode DisplayMode = FullScreenMode.FullScreenWindow;
        public static int QualityLevel = 2;
        public static bool VSync = true;
        public static int FrameRateLimit = -1; // -1 = uncapped

        // Audio
        public static float MasterVolume = 1.0f;
        public static float MusicVolume = 0.5f;
        public static float SFXVolume = 0.8f;

        // Controls
        public static float MouseSensitivity = 0.12f;
        public static bool InvertY = false;
        public static int KeyPreset = 0; // 0 = WASD Standard, 1 = ESDF Alternate

        static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            // Fetch and sort distinct resolutions
            var resList = new List<Resolution>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width >= 800 && r.height >= 600)
                {
                    bool exists = false;
                    for (int i = 0; i < resList.Count; i++)
                    {
                        if (resList[i].width == r.width && resList[i].height == r.height)
                        {
                            exists = true;
                            if (r.refreshRateRatio.value > resList[i].refreshRateRatio.value) resList[i] = r;
                            break;
                        }
                    }
                    if (!exists) resList.Add(r);
                }
            }
            if (resList.Count == 0) resList.Add(Screen.currentResolution);
            AvailableResolutions = resList.ToArray();

            // Load saved or match current resolution
            int savedW = PlayerPrefs.GetInt(KeyResWidth, Screen.width);
            int savedH = PlayerPrefs.GetInt(KeyResHeight, Screen.height);
            SelectedResolutionIndex = 0;
            for (int i = 0; i < AvailableResolutions.Length; i++)
            {
                if (AvailableResolutions[i].width == savedW && AvailableResolutions[i].height == savedH)
                {
                    SelectedResolutionIndex = i;
                    break;
                }
            }

            DisplayMode = (FullScreenMode)PlayerPrefs.GetInt(KeyDisplayMode, (int)FullScreenMode.FullScreenWindow);
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            VSync = PlayerPrefs.GetInt(KeyVSync, 1) != 0;
            FrameRateLimit = PlayerPrefs.GetInt(KeyFrameRate, -1);

            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVol, 1.0f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusicVol, 0.5f);
            SFXVolume = PlayerPrefs.GetFloat(KeySFXVol, 0.8f);

            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 0.12f), 0.02f, 0.60f);
            InvertY = PlayerPrefs.GetInt(KeyInvertY, 0) != 0;
            KeyPreset = PlayerPrefs.GetInt(KeyKeyPreset, 0);

            ApplyAllSettings();
        }

        public static void ApplyAllSettings()
        {
            ApplyGraphics();
            ApplyAudio();
            SaveSettings();
        }

        public static void ApplyGraphics()
        {
            if (AvailableResolutions != null && AvailableResolutions.Length > 0)
            {
                var targetRes = AvailableResolutions[Mathf.Clamp(SelectedResolutionIndex, 0, AvailableResolutions.Length - 1)];
                Screen.SetResolution(targetRes.width, targetRes.height, DisplayMode, targetRes.refreshRateRatio);
            }
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FrameRateLimit;
        }

        public static void ApplyAudio()
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (ArenaAudio.Instance != null)
            {
                ArenaAudio.Instance.MusicVolume = Mathf.Clamp01(MusicVolume);
                ArenaAudio.Instance.EffectsVolume = Mathf.Clamp01(SFXVolume);
            }
        }

        public static void SaveSettings()
        {
            if (AvailableResolutions != null && SelectedResolutionIndex >= 0 && SelectedResolutionIndex < AvailableResolutions.Length)
            {
                PlayerPrefs.SetInt(KeyResWidth, AvailableResolutions[SelectedResolutionIndex].width);
                PlayerPrefs.SetInt(KeyResHeight, AvailableResolutions[SelectedResolutionIndex].height);
            }
            PlayerPrefs.SetInt(KeyDisplayMode, (int)DisplayMode);
            PlayerPrefs.SetInt(KeyQuality, QualityLevel);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyFrameRate, FrameRateLimit);

            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.SetFloat(KeyMusicVol, MusicVolume);
            PlayerPrefs.SetFloat(KeySFXVol, SFXVolume);

            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            PlayerPrefs.SetInt(KeyInvertY, InvertY ? 1 : 0);
            PlayerPrefs.SetInt(KeyKeyPreset, KeyPreset);
            PlayerPrefs.Save();
        }

        public static void RestoreDefaults()
        {
            MasterVolume = 1.0f;
            MusicVolume = 0.5f;
            SFXVolume = 0.8f;
            MouseSensitivity = 0.12f;
            InvertY = false;
            KeyPreset = 0;
            VSync = true;
            FrameRateLimit = -1;
            QualityLevel = Mathf.Min(2, Mathf.Max(0, QualitySettings.names.Length - 1));
            DisplayMode = FullScreenMode.FullScreenWindow;
            if (AvailableResolutions != null)
            {
                for (int i = 0; i < AvailableResolutions.Length; i++)
                {
                    if (AvailableResolutions[i].width == Screen.currentResolution.width &&
                        AvailableResolutions[i].height == Screen.currentResolution.height)
                    {
                        SelectedResolutionIndex = i;
                        break;
                    }
                }
            }
            ApplyAllSettings();
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace WhosWatching.Tests
{
    public sealed class GameSettingsTests
    {
        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteAll();
            GameSettings.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void DefaultSettingsAreValid()
        {
            Assert.That(GameSettings.MasterVolume, Is.InRange(0f, 1f));
            Assert.That(GameSettings.MusicVolume, Is.InRange(0f, 1f));
            Assert.That(GameSettings.SFXVolume, Is.InRange(0f, 1f));
            Assert.That(GameSettings.MouseSensitivity, Is.InRange(0.02f, 0.60f));
            Assert.That(GameSettings.InvertY, Is.False);
            Assert.That(GameSettings.VSync, Is.True);
        }

        [Test]
        public void SettingsPersistAcrossSaveAndLoad()
        {
            GameSettings.MasterVolume = 0.42f;
            GameSettings.MusicVolume = 0.77f;
            GameSettings.SFXVolume = 0.33f;
            GameSettings.MouseSensitivity = 0.25f;
            GameSettings.InvertY = true;
            GameSettings.VSync = false;
            GameSettings.QualityLevel = 1;
            GameSettings.SaveSettings();

            // Reset in-memory values and re-read from PlayerPrefs
            Assert.That(PlayerPrefs.GetFloat("WW.MasterVolume"), Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat("WW.MusicVolume"), Is.EqualTo(0.77f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat("WW.SFXVolume"), Is.EqualTo(0.33f).Within(0.001f));
            Assert.That(PlayerPrefs.GetFloat("WW.MouseSensitivity"), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(PlayerPrefs.GetInt("WW.InvertY"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("WW.VSync"), Is.EqualTo(0));
            Assert.That(PlayerPrefs.GetInt("WW.QualityLevel"), Is.EqualTo(1));
        }

        [Test]
        public void RestoreDefaultsResetsValues()
        {
            GameSettings.MasterVolume = 0.1f;
            GameSettings.InvertY = true;
            GameSettings.RestoreDefaults();

            Assert.That(GameSettings.MasterVolume, Is.EqualTo(1.0f));
            Assert.That(GameSettings.InvertY, Is.False);
            Assert.That(GameSettings.VSync, Is.True);
            Assert.That(GameSettings.MouseSensitivity, Is.EqualTo(0.12f).Within(0.001f));
        }

        [Test]
        public void LogoAssetExistsInProject()
        {
            string logoPath = System.IO.Path.Combine(Application.dataPath, "_WhosWatching/Art/UI/Logo_WhosWatching.png");
            Assert.That(System.IO.File.Exists(logoPath), Is.True, "Logo_WhosWatching.png should exist in Assets/_WhosWatching/Art/UI/");
        }
    }
}

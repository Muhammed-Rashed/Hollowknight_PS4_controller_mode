using System;
using System.Collections.Generic;
using System.Linq;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using HidSharp;

namespace PS4_mod
{
    public class PS4_mod : Mod
    {
        internal static PS4_mod Instance;
        private DualShockWriter ds4;

        public PS4_mod() : base("PS4_mod") { }

        // Entry point
        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");
            Instance = this;

            // Open the DualShock 4 (USB only)
            ds4 = new DualShockWriter();
            if (!ds4.TryOpen())
            {
                Log("No DualShock 4 found or it's in use. Close DS4Windows/Steam Input.");
            }

            // ---- ModHooks for the current Modding API ----
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;  // scene/location changes
            ModHooks.HeroUpdateHook += UpdateHealthColor;           // runs every frame
            ModHooks.AfterTakeDamageHook += OnTakeDamage;           // when taking damage

            Log("Initialized");
        }

        // Called whenever a new scene loads
        private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            if (!ds4.IsOpen) return;

            string sceneName = newScene.name;

            // Simple location-based color mapping
            Color c = Color.white;
            if (sceneName.Contains("City")) c = Color.gray;
            else if (sceneName.Contains("Fungus")) c = Color.green;
            else if (sceneName.Contains("White_Palace")) c = Color.cyan;

            ds4.SetLight(c);
        }

        // Called when damage is taken
        private int OnTakeDamage(int hazardType, int damageAmount)
        {
            UpdateHealthColor();
            return damageAmount; // must return the (possibly modified) damage amount
        }

        // Called each frame and also when damage is taken
        private void UpdateHealthColor()
        {
            if (!ds4.IsOpen) return;

            // Check if HeroController instance exists
            if (HeroController.instance?.playerData == null) return;

            int cur = HeroController.instance.playerData.health;
            int max = HeroController.instance.playerData.maxHealth;

            // Avoid division by zero
            if (max <= 0) return;

            // Map health ratio to a red → green gradient
            float t = Mathf.Clamp01((float)cur / max);
            Color c = Color.Lerp(Color.red, Color.green, t);

            ds4.SetLight(c);
        }

        // Clean up resources when mod is disposed
        ~PS4_mod()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            ModHooks.HeroUpdateHook -= UpdateHealthColor;
            ModHooks.AfterTakeDamageHook -= OnTakeDamage;

            if (ds4.IsOpen)
            {
                ds4.Close();
            }
        }
    }

    // -------- DualShock 4 HID helper --------
    internal class DualShockWriter
    {
        private HidStream stream;

        public bool IsOpen => stream != null && stream.CanWrite;

        // Try to open the first connected DualShock 4 over USB
        public bool TryOpen()
        {
            try
            {
                // Sony vendor ID = 0x054C, product IDs 0x05C4 (1st gen) or 0x09CC (newer)
                var device = DeviceList.Local.GetHidDevices(0x054C)
                    .FirstOrDefault(d => d.ProductID == 0x05C4 || d.ProductID == 0x09CC);

                if (device == null) return false;

                return device.TryOpen(out stream);
            }
            catch (Exception ex)
            {
                PS4_mod.Instance?.Log($"Error opening DualShock 4: {ex.Message}");
                return false;
            }
        }

        // Send a 32-byte output report to set the LED color
        public void SetLight(Color c)
        {
            if (!IsOpen) return;

            try
            {
                byte r = (byte)(c.r * 255);
                byte g = (byte)(c.g * 255);
                byte b = (byte)(c.b * 255);

                // USB report: 0x05 header, 0xFF flags, RGB in bytes 6–8
                byte[] data = new byte[32];
                data[0] = 0x05;   // report ID
                data[1] = 0xFF;   // enable flags
                data[6] = r;
                data[7] = g;
                data[8] = b;

                stream.Write(data);
            }
            catch (Exception ex)
            {
                PS4_mod.Instance?.Log($"Error writing to DualShock 4: {ex.Message}");
            }
        }

        // Close the HID stream
        public void Close()
        {
            try
            {
                stream?.Close();
                stream?.Dispose();
                stream = null;
            }
            catch (Exception ex)
            {
                PS4_mod.Instance?.Log($"Error closing DualShock 4: {ex.Message}");
            }
        }
    }
}
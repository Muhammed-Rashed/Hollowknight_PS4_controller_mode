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

        private int lastHealth = -1;

        public PS4_mod() : base("PS4_mod") { }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");
            Instance = this;

            ds4 = new DualShockWriter();
            if (!ds4.TryOpen())
            {
                Log("No DualShock 4 found or it's in use. Close DS4Windows/Steam Input.");
            }
            else
            {
                Log("DualShock 4 successfully opened.");
            }

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            ModHooks.AfterTakeDamageHook += OnTakeDamage;

            Log("Initialized");
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            Log($"Scene changed: {oldScene.name} -> {newScene.name}");

            if (!ds4.IsOpen)
            {
                Log("DualShock not open, skipping LED update.");
                return;
            }

            Color c = Color.white;
            if (newScene.name.Contains("City")) c = Color.gray;
            else if (newScene.name.Contains("Fungus")) c = Color.green;
            else if (newScene.name.Contains("White_Palace")) c = Color.cyan;

            ds4.SetLight(c);
            Log($"LED set to {c}");
        }

        // Called only when damage is taken
        private int OnTakeDamage(int hazardType, int damageAmount)
        {
            UpdateHealthColor();
            return damageAmount;
        }

        private void UpdateHealthColor()
        {
            if (!ds4.IsOpen) return;
            if (HeroController.instance?.playerData == null) return;

            int cur = HeroController.instance.playerData.health;
            int max = HeroController.instance.playerData.maxHealth;
            if (max <= 0) return;

            if (cur == lastHealth) return;
            lastHealth = cur;

            float t = Mathf.Clamp01((float)cur / max);
            Color c = Color.Lerp(Color.red, Color.green, t);
            ds4.SetLight(c);
        }
    }

    internal class DualShockWriter
    {
        private HidDevice device;
        private Color? lastColor;

        public bool IsOpen => device != null;

        public bool TryOpen()
        {
            try
            {
                device = DeviceList.Local.GetHidDevices(0x054C)
                    .FirstOrDefault(d => d.ProductID == 0x05C4 || d.ProductID == 0x09CC);

                return device != null;
            }
            catch (Exception ex)
            {
                PS4_mod.Instance?.Log($"Error finding DualShock 4: {ex.Message}");
                device = null;
                return false;
            }
        }

        public void SetLight(Color c)
        {
            if (device == null) return;

            if (lastColor.HasValue &&
                Mathf.Approximately(lastColor.Value.r, c.r) &&
                Mathf.Approximately(lastColor.Value.g, c.g) &&
                Mathf.Approximately(lastColor.Value.b, c.b))
                return;

            lastColor = c;

            HidStream stream = null;
            try
            {
                if (device.TryOpen(out stream))
                {
                    byte[] data = new byte[32];
                    data[0] = 0x05;
                    data[1] = 0xFF;
                    data[6] = (byte)(c.r * 255);
                    data[7] = (byte)(c.g * 255);
                    data[8] = (byte)(c.b * 255);

                    stream.Write(data);
                    PS4_mod.Instance?.Log($"Sent LED color: R{data[6]} G{data[7]} B{data[8]}");
                }
            }
            catch (Exception ex)
            {
                PS4_mod.Instance?.Log($"Error writing to DualShock 4: {ex.Message}");
            }
            finally
            {
                try
                {
                    stream?.Close();
                    stream?.Dispose();
                }
                catch (Exception ex)
                {
                    PS4_mod.Instance?.Log($"Error closing stream: {ex.Message}");
                }
            }
        }
    }
}
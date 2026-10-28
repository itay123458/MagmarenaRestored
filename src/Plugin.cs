using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmarenaPrivate
{
    [BepInPlugin(Id, "Magmarena Private", "0.1.0")]
    [DefaultExecutionOrder(-32000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.magmarena.private";
        private const string SupportedAssembly = "e0c7fcb26fd064897550fcedc21ddc43e3df7b92204e4aef42110be6dc88a1c1";
        internal static Plugin Instance;
        private Harmony harmony;
        private ArenaBinding binding;
        private bool compatible;
        private readonly RoomDeparture departure = new RoomDeparture();
        private bool activationFailed;
        private bool entranceOpened;
        private bool cleaning;
        private bool cleanupPending;
        private float nextAttempt;
        private float nextWarning;
        private int nativeStartFrame;
        private float nativeStartDeadline;
        private float nextDiagnostic;
        private NetworkSystem activeNetwork;
        private string activeRoom;
        private ConfigEntry<bool> fixLighting;
        private ConfigEntry<float> arenaBrightness, tunnelBrightness;
        private DateTime configWrite;
        private float nextVisual;

        private void Awake()
        {
            Instance = this;
            try
            {
                fixLighting = Config.Bind("Visuals", "FixArenaLighting", true, "Use original arena textures without its broken legacy baked-lighting atlas. Only affects restored private-room objects.");
                arenaBrightness = Config.Bind("Visuals", "ArenaBrightness", 0.75f, new ConfigDescription("Brightness of repaired arena surfaces; changes reload during a private session.", new AcceptableValueRange<float>(0.15f, 1f)));
                tunnelBrightness = Config.Bind("Visuals", "TunnelBrightness", 0.45f, new ConfigDescription("Brightness of repaired tunnel surfaces; changes reload during a private session.", new AcceptableValueRange<float>(0.15f, 1f)));
                configWrite = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(typeof(NetworkSystem).Assembly.Location))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (actual != SupportedAssembly) throw new NotSupportedException("Unsupported game assembly; this release targets Steam build 26061329");
                }
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                SceneManager.sceneUnloaded += SceneUnloaded;
                compatible = true;
                Logger.LogInfo("Magmarena Private loaded. Waiting for a joined private PUN room.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Magmarena remains disabled: " + ex.Message);
                if (harmony != null) harmony.UnpatchSelf();
                enabled = false;
            }
        }

        private bool Allowed()
        {
            if (!compatible || departure.Leaving || !isActiveAndEnabled) return false;
            try
            {
                NetworkSystem network = NetworkSystem.Instance;
                if (network == null) return false;
                // The retained MonkeBall implementation uses native PhotonView RPCs directly.
                // Do not open a nonfunctional arena on an unsupported networking backend.
                if (!(network is NetworkSystemPUN)) return false;
                return RoomPolicy.Allows(true, network.InRoom, network.SessionIsPrivate, network.netState != NetSystemState.InGame);
            }
            catch { return false; }
        }

        private void Update()
        {
            if (cleanupPending) { CloseArena("Retrying cleanup"); return; }
            if (!Allowed()) { CloseArena("Room is not eligible"); return; }
            if (activationFailed) return;
            NetworkSystem network = NetworkSystem.Instance;
            if (binding != null && (activeNetwork != network || activeRoom != network.RoomName))
                CloseArena("Room changed");
            if (binding == null && Time.unscaledTime >= nextAttempt)
            {
                nextAttempt = Time.unscaledTime + 2f;
                try
                {
                    binding = ArenaBinding.Find();
                    activeNetwork = network;
                    activeRoom = network.RoomName;
                    binding.Activation.Apply(false);
                    nativeStartFrame = Time.frameCount + 2;
                    nativeStartDeadline = Time.unscaledTime + 2f;
                    entranceOpened = false;
                    Logger.LogInfo("Preparing original Magmarena gameplay before opening the private entrance.");
                }
                catch (Exception ex)
                {
                    Warn("Cannot restore Magmarena: " + ex.Message);
                    CloseArena("Activation failed");
                }
            }
            if (binding != null && Time.frameCount >= nativeStartFrame)
            {
                try
                {
                    binding.Lifecycle.PollInitialization();
                    if (!binding.Lifecycle.NativeStarted && Time.unscaledTime > nativeStartDeadline)
                        throw new InvalidOperationException("Original ball/scoreboard Start did not complete");
                    if (binding.Lifecycle.Ready)
                    {
                        binding.Activation.Apply(true);
                        if (Time.unscaledTime >= nextVisual)
                        {
                            nextVisual = Time.unscaledTime + 1f;
                            DateTime write = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
                            if (write != configWrite) { Config.Reload(); configWrite = write; }
                            if (fixLighting.Value) binding.Materials.Apply(arenaBrightness.Value, tunnelBrightness.Value);
                            else binding.Materials.Restore();
                        }
                        if (!entranceOpened) { entranceOpened = true; Logger.LogInfo("Magmarena is initialized and synchronized; Forest entrance opened for this private room."); }
                        if (Time.unscaledTime >= nextDiagnostic)
                        {
                            nextDiagnostic = Time.unscaledTime + 2f;
                            try { Diagnostics.Write(binding); } catch (Exception ex) { Warn("Diagnostics: " + ex.Message); }
                        }
                    }
                }
                catch (Exception ex) { activationFailed = true; Warn("Native arena initialization failed: " + ex.Message); CloseArena("Gameplay initialization failed; rejoin to retry"); }
            }
        }

        private void FixedUpdate() { if (cleanupPending || !Allowed()) CloseArena("Room no longer private"); }

        private void LateUpdate()
        {
            if (cleanupPending) { CloseArena("Retrying cleanup"); return; }
            if (!Allowed()) { CloseArena("Room no longer private"); return; }
            if (binding == null) return;
            try { binding.Activation.Apply(binding.Lifecycle.Ready); }
            catch (Exception ex) { Warn("Arena restoration interrupted: " + ex.Message); CloseArena("Scene objects changed"); }
        }

        private void Warn(string message)
        {
            if (Time.unscaledTime < nextWarning) return;
            nextWarning = Time.unscaledTime + 10f;
            Logger.LogWarning(message);
        }

        internal void BeginLeaving(NetworkSystem source)
        {
            departure.Begin(source, NetworkSystem.Instance, delegate { CloseArena("Leaving private room"); });
        }

        internal bool RestorationAllowed { get { return Allowed() && binding != null && !cleanupPending; } }
        internal void EnteredGame(NetworkSystem source)
        {
            if (!System.Object.ReferenceEquals(source, NetworkSystem.Instance)) return;
            departure.Enter(); activationFailed = false; nextAttempt = 0f;
        }

        private void CloseArena(string reason)
        {
            if (binding == null || cleaning) return;
            cleaning = true;
            cleanupPending = true;
            try
            {
                bool restored = CleanupSequence.Run(
                    delegate { return CleanupStep("Visual cleanup", binding.Materials.Restore); },
                    delegate { return CleanupStep("Forest return", binding.ReturnToForest); },
                    delegate { return CleanupStep("Native arena cleanup", binding.Lifecycle.Suspend); },
                    delegate { return binding.Activation.Restore(); });
                if (!restored)
                {
                    if (binding.Activation.LastError != null) Warn("Arena cleanup will retry: " + binding.Activation.LastError.Message);
                    return;
                }
                binding = null;
                activeNetwork = null;
                activeRoom = null;
                cleanupPending = false;
                nextAttempt = Time.unscaledTime + 2f;
                Logger.LogInfo("Magmarena closed: " + reason);
            }
            finally { cleaning = false; }
        }

        private bool CleanupStep(string phase, Action action)
        {
            try { action(); return true; }
            catch (Exception ex) { Warn(phase + ": " + ex.Message); return false; }
        }

        private void SceneUnloaded(Scene scene)
        {
            if (binding != null && scene.handle == binding.SceneHandle) CloseArena("Original scene unloaded");
        }

        private void OnDisable() { CloseArena("Plugin disabled"); }

        private void OnDestroy()
        {
            CloseArena("Plugin destroyed");
            SceneManager.sceneUnloaded -= SceneUnloaded;
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(NetworkSystemPUN), "OnDisconnected")]
    internal static class UnexpectedDisconnection
    {
        [HarmonyPrefix]
        private static void Prefix(NetworkSystemPUN __instance)
        {
            if (Plugin.Instance != null && System.Object.ReferenceEquals(NetworkSystem.Instance, __instance))
                Plugin.Instance.BeginLeaving(__instance);
        }
    }

    [HarmonyPatch(typeof(NetworkSystem), "PreLeavingRoom")]
    internal static class BeforeRoomDeparture
    {
        [HarmonyPrefix]
        private static void Prefix(NetworkSystem __instance) { if (Plugin.Instance != null) Plugin.Instance.BeginLeaving(__instance); }
    }

    [HarmonyPatch(typeof(NetworkSystem), "set_netState")]
    internal static class NetworkStateChanged
    {
        [HarmonyPrefix]
        private static void Prefix(NetworkSystem __instance, NetSystemState __0)
        {
            if (__0 != NetSystemState.InGame && Plugin.Instance != null) Plugin.Instance.BeginLeaving(__instance);
        }
        [HarmonyPostfix]
        private static void Postfix(NetworkSystem __instance, NetSystemState __0)
        {
            if (__0 == NetSystemState.InGame && Plugin.Instance != null) Plugin.Instance.EnteredGame(__instance);
        }
    }
}

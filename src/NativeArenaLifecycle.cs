using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Object = System.Object;

namespace MagmarenaPrivate
{
    internal sealed class NativeArenaLifecycle
    {
        private readonly MonkeBallGame game;
        private readonly GameBallManager balls;
        private readonly NetworkSystem owner;
        private readonly Action<NetPlayer> joined;
        private readonly Action<NetPlayer> left;
        private readonly Action<NetPlayer> masterChanged;
        private readonly OwnedSubscription<NetworkSystem> subscriptions;
        private readonly FieldInfo hasStoredColor;
        private readonly FieldInfo storedColor;
        private readonly FieldInfo registeredBalls;
        private readonly FieldInfo masterEvent;
        private readonly FieldInfo scoreboardGame;
        private readonly NativeStateSync sync;
        private bool initialized;
        private bool nativeStarted;
        private bool matchReset;
        public bool Ready { get { return nativeStarted && sync.Ready; } }
        public bool NativeStarted { get { return nativeStarted; } }

        public NativeArenaLifecycle(MonkeBallGame game, GameBallManager balls)
        {
            this.game = game; this.balls = balls; owner = NetworkSystem.Instance;
            if (Object.ReferenceEquals(owner, null)) throw new InvalidOperationException("Network owner disappeared");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            joined = Handler(game, "OnPlayerJoined");
            left = Handler(game, "OnPlayerLeft");
            masterChanged = Handler(game, "OnMasterClientSwitched");
            subscriptions = new OwnedSubscription<NetworkSystem>(owner, AttachHandlers, DetachHandlers);
            hasStoredColor = typeof(MonkeBallGame).GetField("_setStoredLocalPlayerColor", flags);
            storedColor = typeof(MonkeBallGame).GetField("_storedLocalPlayerColor", flags);
            registeredBalls = typeof(GameBallManager).GetField("gameBalls", flags);
            masterEvent = typeof(NetworkSystem).GetField("OnMasterClientSwitchedEvent", flags);
            scoreboardGame = typeof(MonkeBallScoreboard).GetField("game", flags);
            if (hasStoredColor == null || storedColor == null || registeredBalls == null || masterEvent == null || scoreboardGame == null)
                throw new NotSupportedException("Native arena lifecycle changed");
            sync = game.GetComponent<NativeStateSync>();
            if (sync == null) sync = game.gameObject.AddComponent<NativeStateSync>();
        }

        public static bool HasNeutralSpawn(MonkeBallGame game)
        {
            FieldInfo field = typeof(MonkeBallGame).GetField("_neutralBallStartLocation", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.GetValue(game) as Transform != null;
        }

        private static Action<NetPlayer> Handler(MonkeBallGame game, string name)
        {
            MethodInfo method = typeof(MonkeBallGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new NotSupportedException("Missing native callback: " + name);
            return (Action<NetPlayer>)Delegate.CreateDelegate(typeof(Action<NetPlayer>), game, method);
        }

        private void AttachHandlers(NetworkSystem network)
        {
            network.OnPlayerJoined += joined;
            network.OnPlayerLeft += left;
            ChangeMasterHandler(network, true);
        }

        private void DetachHandlers(NetworkSystem network)
        {
            // These are managed fields: detach from the captured owner even after Unity fake-null/destruction.
            network.OnPlayerJoined -= joined;
            network.OnPlayerLeft -= left;
            ChangeMasterHandler(network, false);
        }

        private void ChangeMasterHandler(NetworkSystem network, bool add)
        {
            GorillaTag.DelegateListProcessor<NetPlayer> callbacks = (GorillaTag.DelegateListProcessor<NetPlayer>)masterEvent.GetValue(network);
            if (add) callbacks += masterChanged;
            else callbacks -= masterChanged;
            masterEvent.SetValue(network, callbacks);
        }

        public void Resume()
        {
            subscriptions.Detach(); // Also removes any registrations made by the just-completed native Awake.
            NetworkSystem current = NetworkSystem.Instance;
            if (!Object.ReferenceEquals(current, owner))
            {
                if (!Object.ReferenceEquals(current, null)) DetachHandlers(current);
                throw new InvalidOperationException("Network owner changed during arena initialization");
            }
            if (game == null || balls == null || MonkeBallGame.Instance != game || GameBallManager.Instance != balls)
                throw new InvalidOperationException("Original arena managers did not initialize or have conflicting owners");
            initialized = true;
            subscriptions.Attach();
            nativeStarted = false; matchReset = false;
        }

        public void PollInitialization()
        {
            if (!initialized || game == null || !game.isActiveAndEnabled) return;
            if (!nativeStarted)
            {
                IList entries = registeredBalls.GetValue(balls) as IList;
                if (entries == null || !entries.Contains(game.startingBalls[0].gameBall)) return;
                foreach (MonkeBallScoreboard board in game.scoreboards)
                    if (board == null || !Object.ReferenceEquals(scoreboardGame.GetValue(board), game)) return;
                nativeStarted = true;
                sync.Configure(game, owner, joined);
            }
            sync.Poll();
            if (Ready && !matchReset && owner.IsMasterClient && ZoneManagement.IsInZone(GTZone.arena))
            {
                game.RequestResetGame(); matchReset = true;
            }
        }

        public void RestoreAppearance()
        {
            if (!initialized || game == null || !(bool)hasStoredColor.GetValue(game)) return;
            NetworkSystem network = NetworkSystem.Instance;
            if (Object.ReferenceEquals(network, owner) && network.InRoom && network.SessionIsPrivate && ZoneManagement.IsInZone(GTZone.arena))
                game.RequestSetTeam(-1);
            if (!(bool)hasStoredColor.GetValue(game)) return;
            Color original = (Color)storedColor.GetValue(game);
            game.OnPlayerDestroy(); hasStoredColor.SetValue(game, false);
            if (GorillaTagger.Instance != null) GorillaTagger.Instance.UpdateColor(original.r, original.g, original.b);
            if (GorillaNetworking.GorillaComputer.instance != null)
                GorillaNetworking.GorillaComputer.instance.UpdateColor(original.r, original.g, original.b);
            if (GorillaNetworking.CosmeticsController.instance != null)
                GorillaNetworking.CosmeticsController.instance.UpdateWornCosmetics(true);
        }

        public void Suspend()
        {
            try { RestoreAppearance(); }
            finally
            {
                if (!Object.ReferenceEquals(sync, null)) sync.StopManaged();
                subscriptions.Detach();
                nativeStarted = false; matchReset = false;
            }
        }
    }
}

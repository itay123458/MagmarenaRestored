using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace MagmarenaPrivate
{
    public sealed class NativeStateSync : MonoBehaviour
    {
        private const string RequestRpc = "MagmarenaPrivateRequestState";
        private readonly StateSyncTracker tracker = new StateSyncTracker();
        private readonly Dictionary<int, float> lastRequest = new Dictionary<int, float>();
        private MonkeBallGame game;
        private NetworkSystem owner;
        private string room;
        private Action<NetPlayer> sendNativeSnapshot;
        private bool configured;
        internal bool Ready { get { return configured && tracker.Ready; } }

        internal void Configure(MonkeBallGame game, NetworkSystem owner, Action<NetPlayer> sendNativeSnapshot)
        {
            this.game = game; this.owner = owner; this.sendNativeSnapshot = sendNativeSnapshot;
            room = owner.RoomName;
            lastRequest.Clear();
            tracker.Begin(owner.IsMasterClient, Time.unscaledTime);
            configured = true;
            game.photonView.RefreshRpcMonoBehaviourCache();
        }

        private bool Allowed()
        {
            return configured && game != null && Plugin.Instance != null && Plugin.Instance.RestorationAllowed &&
                System.Object.ReferenceEquals(NetworkSystem.Instance, owner) && owner.RoomName == room;
        }

        internal void Poll()
        {
            if (!Allowed()) return;
            if (tracker.TimedOut(Time.unscaledTime))
                throw new InvalidOperationException("The private room's master did not synchronize Magmarena; all players including the master need this mod");
            if (tracker.ShouldRequest(Time.unscaledTime)) game.photonView.RPC(RequestRpc, RpcTarget.MasterClient);
        }

        [PunRPC]
        private void MagmarenaPrivateRequestState(PhotonMessageInfo info)
        {
            if (!Allowed() || !tracker.Ready || !owner.IsMasterClient || info.Sender == null || info.photonView != game.photonView) return;
            int actor = info.Sender.ActorNumber;
            NetPlayer player = owner.GetNetPlayerByID(actor);
            if (player == null) return;
            float previous;
            if (lastRequest.TryGetValue(actor, out previous) && Time.unscaledTime - previous < 1.5f) return;
            lastRequest[actor] = Time.unscaledTime;
            // The existing native callback sends its full validated match snapshot to this actor.
            sendNativeSnapshot(player);
        }

        internal void ReceivedNativeSnapshot(int state, double endTime, int[] scores, PhotonMessageInfo info)
        {
            if (!Allowed() || info.Sender == null || info.Sender != PhotonNetwork.MasterClient || info.photonView != game.photonView) return;
            if ((int)game.GetGameState() != state || game.gameEndTime != endTime || scores == null || game.team.Count != scores.Length) return;
            for (int index = 0; index < scores.Length; index++) if (game.team[index].score != scores[index]) return;
            tracker.Snapshot(true, true);
        }

        internal void StopManaged()
        {
            configured = false; tracker.Stop(); lastRequest.Clear();
            owner = null; room = null; sendNativeSnapshot = null;
        }
        private void OnDisable() { StopManaged(); }
    }

    [HarmonyPatch(typeof(MonkeBallGame), "RequestSetGameStateRPC")]
    internal static class NativeSnapshotReceived
    {
        [HarmonyPostfix]
        private static void Postfix(MonkeBallGame __instance, int __0, double __1, int[] __4, PhotonMessageInfo __7)
        {
            if (__instance == null) return;
            NativeStateSync sync = __instance.GetComponent<NativeStateSync>();
            if (sync != null) sync.ReceivedNativeSnapshot(__0, __1, __4, __7);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmarenaPrivate
{
    internal sealed class ArenaBinding
    {
        private const string Local = "Environment Objects/LocalObjects_Prefab/";
        public readonly ArenaActivation Activation;
        public readonly NativeArenaLifecycle Lifecycle;
        public readonly ArenaMaterials Materials;
        public readonly int SceneHandle;
        private readonly Transform forestSpawn;
        private GameObject[] diagnosticTargets;

        private ArenaBinding(GameObject root, Transform forestSpawn, NativeArenaLifecycle lifecycle, ArenaActivation activation, GameObject connector)
        {
            SceneHandle = root.scene.handle;
            this.forestSpawn = forestSpawn;
            Lifecycle = lifecycle;
            Activation = activation;
            Materials = new ArenaMaterials(connector);
        }

        public static ArenaBinding Find()
        {
            GameObject persistent = FindExact(Local + "MonkeBallRoomPersistent");
            GameObject connector = FindExact(Local + "Arena_Connector");
            GameObject entry = FindExact(Local + "Forest/Props/Arena_Connector_Entry");
            GameObject closure = FindExact(Local + "Forest/ArenaClosed");
            GameObject blocker = FindExact(Local + "Arena_Connector/ArenaEntranceBlocker");
            GameObject zone = FindExact("System Scripts/ZoneGraph/Arena");
            Transform spawn = FindExact(Local + "Level Spawn Locations/Forest").transform;
            GameObject forest = FindExact(Local + "Forest");
            GameObject oldWall = FindExact(Local + "Forest/Terrain/pitgeo/pitregularwallV4 Variant (PostHoverboard)");
            MeshCollider sealedWall = FindExact(Local + "Forest/Terrain/pitgeo/pitregularwallV2/pitregularwall").GetComponent<MeshCollider>();
            DoorwayPatch doorway = new DoorwayPatch(forest, oldWall.GetComponent<MeshFilter>(), oldWall.GetComponent<MeshRenderer>(), sealedWall);

            MonkeBallGame game = persistent.GetComponentInChildren<MonkeBallGame>(true);
            GameBallManager balls = persistent.GetComponentInChildren<GameBallManager>(true);
            if (game == null || balls == null || !NativeReferences.Required(game.enabled, balls.enabled,
                    game.resetButton != null, game.BallLauncher != null, NativeArenaLifecycle.HasNeutralSpawn(game)) ||
                !NativeReferences.All(game.startingBalls, 1, 1, delegate(MonkeBall ball) { return ball != null && ball.enabled && ball.gameBall != null && ball.gameBall.enabled; }) ||
                !NativeReferences.All(game.goalZones, 2, 2, delegate(MonkeBallGoalZone goal) { return goal != null && goal.enabled; }) ||
                !NativeReferences.All(game.scoreboards, 1, 2, delegate(MonkeBallScoreboard board) { return board != null && board.enabled; }) ||
                !NativeReferences.All(game.shotclocks, 2, 2, delegate(MonkeBallShotclock clock) { return clock != null && clock.enabled; }) ||
                !NativeReferences.All(game.team, 2, 2, delegate(MonkeBallTeam team) { return team != null && team.ballStartLocation != null && team.ballLaunchPosition != null; }) ||
                game.photonView == null)
                throw new InvalidOperationException("Original arena gameplay references are incomplete; restoration stays disabled");

            // Do not take ownership of an arena already opened by another plugin or an updated game.
            if (persistent.activeSelf || connector.activeSelf || entry.activeSelf || !closure.activeSelf || !blocker.activeSelf)
                throw new InvalidOperationException("Original entrance states differ from the supported build; restoration stays disabled");

            NativeArenaLifecycle lifecycle = new NativeArenaLifecycle(game, balls);
            List<ActivationChange> changes = new List<ActivationChange>();
            const string regional = "Environment Objects/TriggerZones_Prefab/ZoneTransitions_Prefab/Regional Transition/";
            TunnelAlignment alignment = new TunnelAlignment(connector, new[] {
                FindExact(Local + "Arena_Connector/Arena_Connector_Prefab").transform,
                FindExact(Local + "Arena_Connector/TeleportNode (orig)").transform,
                FindExact(Local + "Arena_Connector/BoundaryStoneSet - To Forest").transform,
                FindExact(Local + "Arena_Connector/BoundaryStoneSet - To Arena").transform,
                FindExact(regional + "ForestToArena").transform,
                FindExact(regional + "ArenaToForest").transform });
            changes.Add(new ActivationChange(alignment, true));
            changes.Add(new ActivationChange(new UnityActivationTarget(FindExact(Local + "Arena_Connector/BoundaryStoneSet - To Forest")), false));
            changes.Add(new ActivationChange(new UnityActivationTarget(zone), true));
            changes.Add(new ActivationChange(new UnityActivationTarget(connector), true));
            changes.Add(new ActivationChange(new LifecycleActivationTarget(new UnityActivationTarget(persistent), lifecycle.Resume, lifecycle.Suspend), true));
            // The retained promotional board has no current title-data image.
            // Leave that obsolete prop group closed instead of showing a blank board.
            // Open the two closures only after the original map and gameplay have been enabled.
            ActivationSession entrance = new ActivationSession(new[] {
                new ActivationChange(doorway, true),
                new ActivationChange(new UnityActivationTarget(closure), false),
                new ActivationChange(new UnityActivationTarget(blocker), false) });
            ArenaBinding binding = new ArenaBinding(persistent, spawn, lifecycle, new ArenaActivation(new ActivationSession(changes), entrance), connector);
            binding.diagnosticTargets = new[] { persistent, connector, entry, closure, blocker, zone,
                FindExact(Local + "Arena_Connector/Arena_Connector_Prefab"),
                FindExact(Local + "Arena_Connector/TeleportNode (orig)"),
                FindExact(Local + "Arena_Connector/BoundaryStoneSet - To Forest"),
                FindExact(regional + "ForestToArena"), FindExact(regional + "ArenaToForest") };
            return binding;
        }

        public void Describe(StringBuilder text)
        {
            Materials.Describe(text);
            foreach (GameObject target in diagnosticTargets)
            {
                if (target == null) { text.AppendLine("Target destroyed"); continue; }
                text.AppendLine("Target: " + Diagnostics.Path(target.transform) + " self=" + target.activeSelf + " hierarchy=" + target.activeInHierarchy + " pos=" + target.transform.position);
                foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
                    if (target.name == "Arena_Connector_Entry" || target.name == "Arena_Connector")
                        text.AppendLine("  Renderer: " + Diagnostics.Path(renderer.transform) + " enabled=" + renderer.enabled + " bounds=" + renderer.bounds + " material=" + (renderer.sharedMaterial == null ? "null" : renderer.sharedMaterial.name));
            }
        }

        public void ReturnToForest()
        {
            if (!ZoneManagement.IsInZone(GTZone.arena)) return;
            Lifecycle.RestoreAppearance();
            ZoneManagement.SetActiveZone(GTZone.forest);
            if (forestSpawn != null && GorillaLocomotion.GTPlayer.Instance != null)
                GorillaLocomotion.GTPlayer.Instance.TeleportTo(forestSpawn, false, false);
        }

        private static GameObject FindExact(string path)
        {
            int split = path.IndexOf('/');
            string rootName = split < 0 ? path : path.Substring(0, split);
            string remainder = split < 0 ? null : path.Substring(split + 1);
            GameObject found = null;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != rootName) continue;
                    Transform candidate = remainder == null ? root.transform : root.transform.Find(remainder);
                    if (candidate == null) continue;
                    if (found != null) throw new InvalidOperationException("Ambiguous scene path: " + path);
                    found = candidate.gameObject;
                }
            }
            if (found == null) throw new InvalidOperationException("Missing original arena object: " + path);
            return found;
        }
    }

    internal sealed class UnityActivationTarget : IActivationTarget
    {
        private readonly GameObject target;
        private readonly string name;
        public UnityActivationTarget(GameObject target)
        {
            this.target = target;
            name = target.name;
        }
        public string Name { get { return name; } }
        public bool IsAlive { get { return target != null; } }
        public bool Active
        {
            get { return target.activeSelf; }
            set
            {
                target.SetActive(value);
            }
        }
    }
}

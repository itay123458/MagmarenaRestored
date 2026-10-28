using System;
using MagmarenaPrivate;

internal static class RuntimeContractTests
{
    private sealed class Owner
    {
        public event Action Joined;
        public void Add(Action handler) { Joined += handler; }
        public void Remove(Action handler) { Joined -= handler; }
        public void Raise() { if (Joined != null) Joined(); }
    }
    private sealed class Reference { public bool Enabled = true; }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        StateSync(); Subscriptions(); References(); UnexpectedDeparture(); EntranceWaitsForNativeReadiness();
        Console.WriteLine("PASS: snapshot handshake/retries, captured-owner subscriptions, native reference checks, immediate departure");
    }
    private static void StateSync()
    {
        StateSyncTracker sync = new StateSyncTracker();
        sync.Begin(false, 10);
        Assert(!sync.Ready && sync.ShouldRequest(10), "Late client did not request a native snapshot");
        Assert(!sync.ShouldRequest(10.1), "Snapshot request was not rate limited");
        sync.Snapshot(false, true); Assert(!sync.Ready, "A non-master snapshot was accepted");
        sync.Snapshot(true, false); Assert(!sync.Ready, "A snapshot from a previous room was accepted");
        Assert(sync.ShouldRequest(12) && sync.ShouldRequest(14), "Missing snapshot was not retried");
        Assert(sync.TimedOut(16) && !sync.ShouldRequest(16), "Unanswered handshake did not stop after three attempts");
        sync.Snapshot(true, true); Assert(sync.Ready && !sync.ShouldRequest(18), "Valid snapshot did not complete synchronization");
        sync.Stop(); sync.Snapshot(true, true);
        Assert(!sync.Ready && !sync.ShouldRequest(20), "Departed client accepted or requested a snapshot");
        sync.Begin(true, 21); Assert(sync.Ready && !sync.ShouldRequest(21), "Master unnecessarily requested its own snapshot");
    }
    private static void Subscriptions()
    {
        Owner original = new Owner(), replacement = new Owner(); int invoked = 0;
        Action handler = delegate { invoked++; };
        original.Add(handler); // Native Awake already subscribed.
        OwnedSubscription<Owner> lease = new OwnedSubscription<Owner>(original,
            delegate(Owner o) { o.Add(handler); }, delegate(Owner o) { o.Remove(handler); });
        lease.Attach(); original.Raise();
        Assert(invoked == 1, "Normalizing the native subscription duplicated it");
        // A destroyed Unity target can still have a managed delegate. Cleanup must not inspect its liveness.
        bool targetDestroyed = true;
        if (targetDestroyed) lease.Detach();
        original.Raise(); replacement.Raise();
        Assert(invoked == 1, "Destruction/owner replacement left a stale callback");
    }
    private static void References()
    {
        Func<Reference, bool> enabled = delegate(Reference r) { return r != null && r.Enabled; };
        Assert(!NativeReferences.All(new Reference[] { null }, 1, 1, enabled), "Null scoreboard passed validation");
        Assert(!NativeReferences.All(new[] { new Reference(), null }, 2, 2, enabled), "Null goal passed validation");
        Assert(!NativeReferences.All(new[] { new Reference { Enabled = false } }, 1, 1, enabled), "Disabled native component passed validation");
        Assert(NativeReferences.All(new[] { new Reference(), new Reference() }, 2, 2, enabled), "Valid native references were rejected");
        Assert(!NativeReferences.Required(true, true, false, true, true), "Missing reset target passed validation");
        Assert(!NativeReferences.Required(false, true, true, true, true), "Disabled game manager passed validation");
        Assert(!NativeReferences.Required(true, false, true, true, true), "Disabled ball manager passed validation");
        Assert(!NativeReferences.Required(true, true, true, false, true), "Missing ball launcher passed validation");
        Assert(!NativeReferences.Required(true, true, true, true, false), "Missing neutral ball spawn passed validation");
    }
    private static void UnexpectedDeparture()
    {
        ActivationTests.Target target = new ActivationTests.Target("entrance", false);
        ActivationSession session = new ActivationSession(new[] { new ActivationChange(target, true) }); session.Apply();
        RoomDeparture departure = new RoomDeparture(); departure.Begin(delegate { session.Restore(); });
        Assert(departure.Leaving && !target.Value, "Unexpected disconnect did not close synchronously without a frame update");
        departure.Enter(); Assert(!departure.Leaving, "Next joined session stayed blocked");
        object oldOwner = new object(), currentOwner = new object();
        departure.Begin(oldOwner, currentOwner, delegate { throw new Exception("Stale owner ran current cleanup"); });
        Assert(!departure.Leaving, "Stale network owner blocked the current private room");
    }
    private static void EntranceWaitsForNativeReadiness()
    {
        ActivationTests.Target root = new ActivationTests.Target("native gameplay", false);
        ActivationTests.Target closure = new ActivationTests.Target("entrance closure", true);
        ArenaActivation activation = new ArenaActivation(
            new ActivationSession(new[] { new ActivationChange(root, true) }),
            new ActivationSession(new[] { new ActivationChange(closure, false) }));
        activation.Apply(false);
        Assert(root.Value && closure.Value, "Entrance opened before native initialization/snapshot completed");
        activation.Apply(true); Assert(!closure.Value, "Ready arena did not open");
        Assert(activation.Restore() && !root.Value && closure.Value, "Two-phase restoration failed cleanup");
    }
}

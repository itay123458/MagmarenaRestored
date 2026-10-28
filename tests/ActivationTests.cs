using System;
using MagmarenaPrivate;

internal static class ActivationTests
{
    internal sealed class Target : IActivationTarget
    {
        public string Name { get; set; }
        public bool IsAlive { get; set; }
        public bool Value;
        public bool ThrowAfterWrite;
        public int Writes;
        public Action<bool> OnWrite;
        public bool Active
        {
            get { return Value; }
            set
            {
                Value = value;
                Writes++;
                if (OnWrite != null) OnWrite(value);
                if (ThrowAfterWrite) { ThrowAfterWrite = false; throw new Exception("activation callback failed"); }
            }
        }
        public Target(string name, bool value) { Name = name; Value = value; IsAlive = true; }
    }

    public static void Run()
    {
        RoundTrip(); PartialFailureRollsBack(); MissingObjectChangesNothing();
        RestoreRetriesFailures(); DestroyedObjectDoesNotBlockOthers();
        PrivateToPublicClosesEntrance();
        DepartureDuringActivationCancelsIt();
        CleanupHookFailureStillClosesObject();
        Console.WriteLine("PASS: activation round trip, rollback, missing/destroyed objects, cleanup retry, public transition");
    }

    private static ActivationSession Session(Target entrance, Target closure)
    {
        return new ActivationSession(new[] { new ActivationChange(entrance, true), new ActivationChange(closure, false) });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void RoundTrip()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        ActivationSession session = Session(entrance, closure);
        session.Apply(); session.Apply();
        Assert(entrance.Value && !closure.Value, "Private activation did not open entrance");
        Assert(entrance.Writes == 1 && closure.Writes == 1, "Repeated activation wrote duplicate changes");
        // Reassert without replacing the original snapshot, even if another native update changes a controlled root.
        entrance.Value = false; session.Apply();
        Assert(entrance.Value, "Activation was not reasserted");
        Assert(session.Restore(), "Round-trip cleanup failed");
        Assert(!entrance.Value && closure.Value, "Cleanup did not restore exact original states");
        int writes = entrance.Writes + closure.Writes;
        Assert(session.Restore() && entrance.Writes + closure.Writes == writes, "Repeated cleanup wrote additional changes");
    }

    private static void PartialFailureRollsBack()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        closure.ThrowAfterWrite = true;
        ActivationSession session = Session(entrance, closure);
        bool threw = false;
        try { session.Apply(); } catch (Exception) { threw = true; }
        Assert(threw, "Failed activation was treated as success");
        Assert(!entrance.Value && closure.Value, "Partial activation left the entrance open");
        Assert(!session.HasPendingChanges, "Successful failure rollback retained stale snapshots");
    }

    private static void MissingObjectChangesNothing()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        closure.IsAlive = false;
        ActivationSession session = Session(entrance, closure);
        bool threw = false;
        try { session.Apply(); } catch (Exception) { threw = true; }
        Assert(threw && entrance.Writes == 0, "Missing required object allowed partial activation");
    }

    private static void RestoreRetriesFailures()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        ActivationSession session = Session(entrance, closure);
        session.Apply(); closure.ThrowAfterWrite = true;
        Assert(!session.Restore(), "Cleanup hid an activation exception");
        Assert(!entrance.Value, "Cleanup stopped before closing the other object");
        Assert(session.HasPendingChanges, "Failed cleanup discarded its retry state");
        Assert(session.Restore() && !session.HasPendingChanges, "Cleanup retry failed");
    }

    private static void DestroyedObjectDoesNotBlockOthers()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        ActivationSession session = Session(entrance, closure); session.Apply(); entrance.IsAlive = false;
        Assert(session.Restore() && closure.Value, "Destroyed scene object blocked live cleanup");
    }

    private static void PrivateToPublicClosesEntrance()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        ActivationSession session = Session(entrance, closure);
        if (RoomPolicy.Allows(true, true, true, false)) session.Apply();
        Assert(entrance.Value, "Private room did not activate");
        if (!RoomPolicy.Allows(true, true, false, false)) session.Restore();
        Assert(!entrance.Value && closure.Value, "Changing to public left the entrance open");
    }

    private static void DepartureDuringActivationCancelsIt()
    {
        Target entrance = new Target("entrance", false), closure = new Target("closure", true);
        ActivationSession session = Session(entrance, closure);
        entrance.OnWrite = delegate(bool value) { if (value) session.Restore(); };
        bool cancelled = false;
        try { session.Apply(); } catch (OperationCanceledException) { cancelled = true; }
        Assert(cancelled && !entrance.Value && closure.Value, "A departure callback during activation reopened the public entrance");
        Assert(!session.HasPendingChanges, "Cancelled activation retained successfully restored changes");
    }

    private static void CleanupHookFailureStillClosesObject()
    {
        Target target = new Target("gameplay", false);
        IActivationTarget wrapped = new LifecycleActivationTarget(target, delegate { }, delegate { throw new Exception("native cleanup failed"); });
        ActivationSession session = new ActivationSession(new[] { new ActivationChange(wrapped, true) });
        session.Apply();
        Assert(!session.Restore(), "Native cleanup failure went unreported");
        Assert(!target.Value, "Native cleanup failure left gameplay enabled");
    }
}

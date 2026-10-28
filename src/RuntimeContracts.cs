using System;
using System.Collections.Generic;

namespace MagmarenaPrivate
{
    internal sealed class RoomDeparture
    {
        public bool Leaving { get; private set; }
        public void Begin(Action cleanup) { Leaving = true; cleanup(); }
        public void Begin(object source, object current, Action cleanup)
        { if (Object.ReferenceEquals(source, current)) Begin(cleanup); }
        public void Enter() { Leaving = false; }
    }

    internal sealed class OwnedSubscription<T>
    {
        private readonly T owner;
        private readonly Action<T> attach;
        private readonly Action<T> detach;
        public OwnedSubscription(T owner, Action<T> attach, Action<T> detach)
        { this.owner = owner; this.attach = attach; this.detach = detach; }
        public void Attach() { detach(owner); attach(owner); }
        public void Detach() { detach(owner); }
    }

    internal static class NativeReferences
    {
        public static bool All<T>(IList<T> entries, int minimum, int maximum, Func<T, bool> valid)
        {
            if (entries == null || entries.Count < minimum || entries.Count > maximum || valid == null) return false;
            foreach (T entry in entries) if (!valid(entry)) return false;
            return true;
        }
        public static bool Required(bool gameEnabled, bool ballsEnabled, bool reset, bool launcher, bool neutral)
        { return gameEnabled && ballsEnabled && reset && launcher && neutral; }
    }

    internal sealed class StateSyncTracker
    {
        private bool started;
        private int attempts;
        private double nextRequest;
        public bool Ready { get; private set; }
        public void Begin(bool master, double now)
        { started = true; Ready = master; attempts = 0; nextRequest = now; }
        public bool ShouldRequest(double now)
        {
            if (!started || Ready || attempts >= 3 || now < nextRequest) return false;
            attempts++; nextRequest = now + 2; return true;
        }
        public bool TimedOut(double now) { return started && !Ready && attempts >= 3 && now >= nextRequest; }
        public void Snapshot(bool master, bool sameRoom) { if (started && master && sameRoom) Ready = true; }
        public void Stop() { started = false; Ready = false; attempts = 0; }
    }
}

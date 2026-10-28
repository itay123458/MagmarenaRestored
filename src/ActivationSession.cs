using System;
using System.Collections.Generic;

namespace MagmarenaPrivate
{
    internal interface IActivationTarget
    {
        string Name { get; }
        bool IsAlive { get; }
        bool Active { get; set; }
    }

    internal sealed class ActivationChange
    {
        public readonly IActivationTarget Target;
        public readonly bool Desired;
        public ActivationChange(IActivationTarget target, bool desired) { Target = target; Desired = desired; }
    }

    internal sealed class ActivationSession
    {
        private sealed class Snapshot
        {
            public IActivationTarget Target;
            public bool Original;
        }

        private readonly List<ActivationChange> changes;
        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private bool applied;
        private bool applying;
        private bool restoreRequested;

        public ActivationSession(IList<ActivationChange> changes)
        {
            if (changes == null || changes.Count == 0) throw new ArgumentException("No restoration targets supplied");
            this.changes = new List<ActivationChange>(changes);
        }

        public bool HasPendingChanges { get { return snapshots.Count != 0; } }
        public Exception LastError { get; private set; }

        public void Apply()
        {
            if (applying) throw new InvalidOperationException("Arena activation is already running");
            if (!applied && HasPendingChanges) throw new InvalidOperationException("Previous restoration cleanup is incomplete");
            applying = true;
            restoreRequested = false;
            try
            {
                foreach (ActivationChange change in changes)
                    if (change == null || change.Target == null || !change.Target.IsAlive)
                        throw new InvalidOperationException("A required arena object is missing");

                if (!applied)
                {
                    foreach (ActivationChange change in changes)
                        snapshots.Add(new Snapshot { Target = change.Target, Original = change.Target.Active });
                }

                foreach (ActivationChange change in changes)
                {
                    ThrowIfCancelled();
                    if (change.Target.Active != change.Desired) change.Target.Active = change.Desired;
                    ThrowIfCancelled();
                }
                applied = true;
                LastError = null;
            }
            catch
            {
                // Snapshots precede every write, including setters whose callbacks throw after changing state.
                applying = false;
                Restore();
                throw;
            }
            finally { applying = false; }
        }

        private void ThrowIfCancelled()
        {
            if (restoreRequested) throw new OperationCanceledException("Room departure cancelled arena activation");
        }

        public bool Restore()
        {
            // Unity activation can synchronously invoke room callbacks. Never let their cleanup
            // erase snapshots while the interrupted activation is still using them.
            if (applying)
            {
                restoreRequested = true;
                LastError = new OperationCanceledException("Activation is unwinding before cleanup");
                return false;
            }
            applied = false;
            bool success = true;
            LastError = null;
            for (int index = snapshots.Count - 1; index >= 0; index--)
            {
                Snapshot snapshot = snapshots[index];
                try
                {
                    if (snapshot.Target.IsAlive && snapshot.Target.Active != snapshot.Original)
                        snapshot.Target.Active = snapshot.Original;
                    snapshots.RemoveAt(index);
                }
                catch (Exception ex)
                {
                    // Keep only failed entries for a later cleanup attempt, and still clean up the others.
                    LastError = ex;
                    success = false;
                }
            }
            return success;
        }
    }
}

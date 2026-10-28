using System;

namespace MagmarenaPrivate
{
    internal sealed class ArenaActivation
    {
        private readonly ActivationSession prepare;
        private readonly ActivationSession entrance;
        public ArenaActivation(ActivationSession prepare, ActivationSession entrance) { this.prepare = prepare; this.entrance = entrance; }
        public Exception LastError { get { return entrance.LastError ?? prepare.LastError; } }
        public void Apply(bool ready) { prepare.Apply(); if (ready) entrance.Apply(); }
        public bool Restore() { bool closed = entrance.Restore(); bool restored = prepare.Restore(); return closed && restored; }
    }
}

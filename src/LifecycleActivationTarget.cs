using System;

namespace MagmarenaPrivate
{
    internal sealed class LifecycleActivationTarget : IActivationTarget
    {
        private readonly IActivationTarget target;
        private readonly Action resume;
        private readonly Action suspend;
        public LifecycleActivationTarget(IActivationTarget target, Action resume, Action suspend)
        {
            this.target = target;
            this.resume = resume;
            this.suspend = suspend;
        }
        public string Name { get { return target.Name; } }
        public bool IsAlive { get { return target.IsAlive; } }
        public bool Active
        {
            get { return target.Active; }
            set
            {
                target.Active = value;
                if (value) resume();
                else suspend();
            }
        }
    }
}

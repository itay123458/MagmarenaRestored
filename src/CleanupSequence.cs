using System;

namespace MagmarenaPrivate
{
    internal static class CleanupSequence
    {
        public static bool Run(params Func<bool>[] stages)
        {
            bool complete = true;
            foreach (Func<bool> stage in stages)
            {
                try { if (!stage()) complete = false; }
                catch { complete = false; }
            }
            return complete;
        }
    }
}

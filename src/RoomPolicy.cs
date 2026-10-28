namespace MagmarenaPrivate
{
    internal static class RoomPolicy
    {
        public static bool Allows(bool known, bool inRoom, bool isPrivate, bool transitioning)
        {
            return known && inRoom && isPrivate && !transitioning;
        }
    }
}

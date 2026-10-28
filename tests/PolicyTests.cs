using System;
using MagmarenaPrivate;

internal static class PolicyTests
{
    // These catch accidental public/offline enablement and failure to allow private play.
    public static void Run()
    {
        Check(false, false, false, false, false);
        Check(false, false, false, true, false);
        Check(false, false, true, false, false);
        Check(false, false, true, true, false);
        Check(false, true, false, false, false);
        Check(false, true, false, true, false);
        Check(false, true, true, false, false);
        Check(false, true, true, true, false);
        Check(true, false, false, false, false);
        Check(true, false, false, true, false);
        Check(true, false, true, false, false);
        Check(true, false, true, true, false);
        Check(true, true, false, false, false);
        Check(true, true, false, true, false);
        Check(true, true, true, true, false);
        Check(true, true, true, false, true);
        Console.WriteLine("PASS: 16 room-state cases (private modes allowed; public/unknown/offline/transitions denied)");
    }

    private static void Check(bool known, bool inRoom, bool isPrivate, bool transitioning, bool expected)
    {
        bool actual = RoomPolicy.Allows(known, inRoom, isPrivate, transitioning);
        if (actual != expected)
            throw new Exception(String.Format("Room policy failed for known={0}, inRoom={1}, private={2}, transitioning={3}: expected {4}, got {5}", known, inRoom, isPrivate, transitioning, expected, actual));
    }
}

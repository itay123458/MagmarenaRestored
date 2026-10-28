using System;
using MagmarenaPrivate;

internal static class CleanupTests
{
    public static void Run()
    {
        bool entranceOpen = true, nativeActive = true;
        int visualAttempts = 0;
        Func<bool> visuals = delegate { if (++visualAttempts == 1) throw new Exception("Renderer restoration failed"); return true; };
        Func<bool> native = delegate { nativeActive = false; return true; };
        Func<bool> entrance = delegate { entranceOpen = false; return true; };
        if (CleanupSequence.Run(visuals, native, entrance) || nativeActive || entranceOpen)
            throw new Exception("Visual failure blocked immediate native/entrance cleanup or was not retained for retry");
        if (!CleanupSequence.Run(visuals, native, entrance) || visualAttempts != 2)
            throw new Exception("Failed visual cleanup could not finish on retry");
        bool lastRan = false;
        if (CleanupSequence.Run(delegate { return false; }, delegate { lastRan = true; return true; }) || !lastRan)
            throw new Exception("An incomplete cleanup stage skipped later stages");
        Console.WriteLine("PASS: visual cleanup failures cannot block privacy cleanup; all stages retry");
    }
}

using System;

internal static class Program
{
    private static int Main()
    {
        try { PolicyTests.Run(); ActivationTests.Run(); RuntimeContractTests.Run(); ClipperTests.Run(); CleanupTests.Run(); TunnelTests.Run(); LightmapTests.Run(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); return 1; }
    }
}

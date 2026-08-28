namespace TradeProof.App.Tests;

public static class TestProgram
{
    public static async Task<int> Main(string[] args)
    {
        string phase = args.Length == 0 ? "all" : args[0].Trim().ToLowerInvariant();

        if (phase is "all" or "phase0")
        {
            Phase0Tests.Run();
        }

        if (phase is "all" or "phase1")
        {
            await Phase1Tests.Run();
        }

        if (phase is "all" or "phase2")
        {
            await Phase2Tests.Run();
        }

        if (phase is not ("all" or "phase0" or "phase1" or "phase2"))
        {
            throw new ArgumentException($"Unknown test phase '{phase}'.");
        }

        Console.WriteLine($"TradeProof {phase} tests passed.");
        return 0;
    }
}

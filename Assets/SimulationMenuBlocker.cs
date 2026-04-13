public static class SimulationMenuBlocker
{
    public static bool IsBlockingScene()
    {
        return !SimulatorStartMenu.HasStarted() || GazeInactivityMenu.IsMenuOpen();
    }
}

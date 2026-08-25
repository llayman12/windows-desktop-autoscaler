namespace DisplayScalingManager.Core.Configuration;

public static class AppPaths {
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayScalingManager");

    public static string StateFilePath => Path.Combine(RootDirectory, "state.json");

    public static string ConfigFilePath => Path.Combine(RootDirectory, "config.json");

    public static string LogsDirectory => Path.Combine(RootDirectory, "Logs");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}

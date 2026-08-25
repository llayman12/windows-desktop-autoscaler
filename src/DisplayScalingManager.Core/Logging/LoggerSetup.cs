using DisplayScalingManager.Core.Configuration;
using Serilog;

namespace DisplayScalingManager.Core.Logging;

public static class LoggerSetup
{
    public static ILogger CreateLogger()
    {
        AppPaths.EnsureDirectoriesExist();

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}

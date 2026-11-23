namespace GamingCafe.ClientAgent.Logging;

public sealed class FileLoggerOptions
{
    public string LogDirectory { get; set; } = "logs";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetainedFileDays { get; set; } = 14;
}

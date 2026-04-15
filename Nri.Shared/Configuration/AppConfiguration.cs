namespace Nri.Shared.Configuration;

public class ServerConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 4600;
    public MongoConfig Mongo { get; set; } = new MongoConfig();
    public LoggingConfig Logging { get; set; } = new LoggingConfig();
    public TokenConfig Tokens { get; set; } = new TokenConfig();
    public BootstrapAdminConfig BootstrapAdmin { get; set; } = new BootstrapAdminConfig();
    public string AudioFolderPath { get; set; } = "./audio";
}

public class BootstrapAdminConfig
{
    public bool Enabled { get; set; }
    public string Login { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public bool PromoteExistingUser { get; set; } = true;
}

public class MongoConfig
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "nri_system";
}

public class LoggingConfig
{
    public string DebugLogPath { get; set; } = "./logs/debug.log";
    public string SessionLogPath { get; set; } = "./logs/session.log";
    public string AdminLogPath { get; set; } = "./logs/admin.log";
    public string AuditLogPath { get; set; } = "./logs/audit.log";
}

public class TokenConfig
{
    public int TokenLifetimeHours { get; set; } = 12;
}

public class ClientConfig
{
    public string ServerHost { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 4600;
    public bool RememberLastUser { get; set; } = true;
    public bool PreserveClientLogs { get; set; }
}

public class UpdaterConfig
{
    public string UpdateFeedUrl { get; set; } = "https://example.invalid/nri/updates";
    public string InstallFolder { get; set; } = "./client";
    public string Channel { get; set; } = "stable";
    public string LocalVersionFile { get; set; } = "./client/version.txt";
    public string LaunchTarget { get; set; } = "Player";
    public string AdminClientExecutable { get; set; } = "./client/Nri.AdminClient.exe";
    public string PlayerClientExecutable { get; set; } = "./client/Nri.PlayerClient.exe";
}

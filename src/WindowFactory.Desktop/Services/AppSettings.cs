using System.Text.Json;

namespace WindowFactory.Desktop.Services;

/// <summary>Строка подключения: переменная WINDOWFACTORY_DB либо appsettings.json.</summary>
public static class AppSettings
{
    public const string EnvVariable = "WINDOWFACTORY_DB";

    public static string ConnectionString
    {
        get
        {
            var env = Environment.GetEnvironmentVariable(EnvVariable);
            if (!string.IsNullOrWhiteSpace(env)) return env;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                return doc.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString()!;
            }
            catch
            {
                return "Server=localhost;Port=3306;Database=window_factory;User=root;Password=;CharSet=utf8mb4;";
            }
        }
    }
}

namespace TourismApp.Lab2;

public static class Config
{
    public static string Postgres =>
        Environment.GetEnvironmentVariable("PG_CONN")
        ?? "Host=localhost;Port=5433;Database=tourism;Username=postgres;Password=postgres;Maximum Pool Size=32;No Reset On Close=true";

    public static string Mongo =>
        Environment.GetEnvironmentVariable("MONGO_CONN") ?? "mongodb://localhost:27018/?maxPoolSize=32";

    public static string MongoDatabase =>
        Environment.GetEnvironmentVariable("MONGO_DB") ?? "tourism";

    public static string Redis =>
        Environment.GetEnvironmentVariable("REDIS_CONN") ?? "localhost:6380";

    public static string ResultsDirectory =>
        Environment.GetEnvironmentVariable("RESULTS_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "results"));
}

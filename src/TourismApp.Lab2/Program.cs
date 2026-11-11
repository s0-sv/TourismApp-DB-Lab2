using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Npgsql;
using StackExchange.Redis;
using TourismApp.Lab2;
using TourismApp.Lab2.Benchmark;
using TourismApp.Lab2.Data;
using TourismApp.Lab2.Mongo;
using TourismApp.Lab2.RedisLab;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = Encoding.UTF8;

var command = args.FirstOrDefault() ?? "all";
var tours = ArgInt("--tours", 20000);
var scale = ArgDouble("--scale", 1.0);

await using var pg = NpgsqlDataSource.Create(Config.Postgres);
var mongo = new MongoContext(Config.Mongo, Config.MongoDatabase);
Directory.CreateDirectory(Config.ResultsDirectory);

var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

if (command is "seed" or "all")
{
    var sw = Stopwatch.StartNew();
    var dataset = new DatasetGenerator(tours: tours, tourists: Math.Max(500, tours / 4), bookings: tours * 3 / 2).Generate();
    Console.WriteLine($"Згенеровано: {dataset.Tours.Count} турів, {dataset.Reviews.Count} відгуків, {dataset.TourLocations.Count} точок маршрутів ({sw.Elapsed.TotalSeconds:F1} с)");

    sw.Restart();
    await new PostgresSeeder(pg).SeedAsync(dataset);
    Console.WriteLine($"PostgreSQL заповнено за {sw.Elapsed.TotalSeconds:F1} с");

    sw.Restart();
    await new MongoSeeder(mongo).SeedAsync(dataset);
    Console.WriteLine($"MongoDB заповнено за {sw.Elapsed.TotalSeconds:F1} с");
}

if (command is "bench" or "all")
{
    var report = await new BenchmarkRunner(pg, mongo, scale).RunAsync();
    var path = Path.Combine(Config.ResultsDirectory, "benchmark_results.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, json));
    await File.WriteAllTextAsync(Path.Combine(Config.ResultsDirectory, "benchmark_results.md"), ResultsMarkdown.Build(report));
    Console.WriteLine($"\nРезультати збережено: {path}");
}

if (command is "redis" or "all")
{
    await using var mux = await ConnectionMultiplexer.ConnectAsync(Config.Redis + ",allowAdmin=true");
    var log = await new RedisDemo(mux, pg).RunAsync();
    await File.WriteAllTextAsync(Path.Combine(Config.ResultsDirectory, "redis_demo_output.md"), string.Join("\n\n", log) + "\n");
}

int ArgInt(string name, int fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : fallback;
}

double ArgDouble(string name, double fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}

static class ResultsMarkdown
{
    public static string Build(BenchmarkReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Результати тестування швидкодії ({r.StartedAt:yyyy-MM-dd HH:mm})");
        sb.AppendLine();
        sb.AppendLine("## Середовище");
        foreach (var (k, v) in r.Environment) sb.AppendLine($"- {k}: {v}");
        sb.AppendLine();
        sb.AppendLine("## Обсяг даних");
        foreach (var (k, v) in r.DatasetSize) sb.AppendLine($"- {k}: {v:N0}");
        sb.AppendLine();
        sb.AppendLine("## Перевірка еквівалентності");
        foreach (var c in r.ConsistencyChecks) sb.AppendLine($"- {c}");
        sb.AppendLine();
        sb.AppendLine("## Затримки (мс) та пропускна здатність");
        foreach (var g in r.Results.GroupBy(x => x.Scenario))
        {
            sb.AppendLine();
            sb.AppendLine($"### {g.Key}");
            sb.AppendLine();
            sb.AppendLine("| СУБД | Операцій | Клієнтів | Середнє | Медіана | p95 | p99 | op/s |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var m in g)
                sb.AppendLine($"| {m.Engine} | {m.Operations} | {m.Concurrency} | {m.MeanMs:F3} | {m.MedianMs:F3} | {m.P95Ms:F3} | {m.P99Ms:F3} | {m.OpsPerSecond:F0} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Обсяг сховища");
        sb.AppendLine();
        sb.AppendLine("| СУБД | Об'єкт | Записів | Дані, КБ | Індекси, КБ |");
        sb.AppendLine("|---|---|---:|---:|---:|");
        foreach (var s in r.Storage)
            sb.AppendLine($"| {s.Engine} | {s.Object} | {s.Rows:N0} | {s.DataBytes / 1024:N0} | {s.IndexBytes / 1024:N0} |");
        foreach (var g in r.Storage.GroupBy(s => s.Engine))
            sb.AppendLine($"| **{g.Key}** | **разом** | | **{g.Sum(s => s.DataBytes) / 1024:N0}** | **{g.Sum(s => s.IndexBytes) / 1024:N0}** |");
        return sb.ToString();
    }
}

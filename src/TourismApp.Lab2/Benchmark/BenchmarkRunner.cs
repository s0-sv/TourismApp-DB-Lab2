using System.Diagnostics;
using System.Text.Json;
using Dapper;
using MongoDB.Bson;
using Npgsql;
using TourismApp.Lab2.Data;
using TourismApp.Lab2.Domain;
using TourismApp.Lab2.Mongo;
using TourismApp.Lab2.Repositories;

namespace TourismApp.Lab2.Benchmark;

public class Measurement
{
    public string Scenario { get; set; } = "";
    public string Engine { get; set; } = "";
    public int Operations { get; set; }
    public int Concurrency { get; set; } = 1;
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double OpsPerSecond { get; set; }

    public static Measurement From(string scenario, string engine, List<double> samples, double wallSeconds, int concurrency)
    {
        samples.Sort();
        double Pct(double p) => samples[Math.Min(samples.Count - 1, (int)Math.Ceiling(p * samples.Count) - 1)];
        return new Measurement
        {
            Scenario = scenario,
            Engine = engine,
            Operations = samples.Count,
            Concurrency = concurrency,
            MeanMs = samples.Average(),
            MedianMs = Pct(0.50),
            P95Ms = Pct(0.95),
            P99Ms = Pct(0.99),
            MinMs = samples[0],
            MaxMs = samples[^1],
            OpsPerSecond = samples.Count / wallSeconds
        };
    }
}

public class StorageInfo
{
    public string Engine { get; set; } = "";
    public string Object { get; set; } = "";
    public long Rows { get; set; }
    public long DataBytes { get; set; }
    public long IndexBytes { get; set; }
}

public class BenchmarkReport
{
    public DateTime StartedAt { get; set; }
    public Dictionary<string, string> Environment { get; set; } = new();
    public Dictionary<string, long> DatasetSize { get; set; } = new();
    public List<string> ConsistencyChecks { get; set; } = new();
    public List<Measurement> Results { get; set; } = new();
    public List<StorageInfo> Storage { get; set; } = new();
}

public class BenchmarkRunner
{
    private readonly NpgsqlDataSource _pg;
    private readonly MongoContext _mongo;
    private readonly double _scale;
    private readonly Random _rnd = new(2026);
    private readonly BenchmarkReport _report = new() { StartedAt = DateTime.Now };

    public BenchmarkRunner(NpgsqlDataSource pg, MongoContext mongo, double scale = 1.0)
    {
        _pg = pg;
        _mongo = mongo;
        _scale = scale;
    }

    private int N(int n) => Math.Max(10, (int)(n * _scale));

    public async Task<BenchmarkReport> RunAsync()
    {
        var sqlMulti = new SqlTourCatalogRepository(_pg, SqlReadMode.MultipleQueries);
        var sqlJson = new SqlTourCatalogRepository(_pg, SqlReadMode.JsonAggregation);
        var mongo = await MongoTourCatalogRepository.CreateAsync(_mongo);

        await CollectEnvironmentAsync();

        await using var conn = await _pg.OpenConnectionAsync();
        var activeIds = (await conn.QueryAsync<int>("SELECT id FROM tours WHERE is_deleted = FALSE ORDER BY id")).ToArray();
        var countries = (await conn.QueryAsync<string>("SELECT DISTINCT country FROM locations ORDER BY country")).ToArray();
        var categoryIds = (await conn.QueryAsync<int>("SELECT id FROM tour_categories ORDER BY id")).ToArray();
        var locationIds = (await conn.QueryAsync<int>("SELECT id FROM locations ORDER BY id")).ToArray();
        var hotelIds = (await conn.QueryAsync<int>("SELECT id FROM hotels ORDER BY id")).ToArray();
        var guideIds = (await conn.QueryAsync<int>("SELECT id FROM guides ORDER BY id")).ToArray();
        var touristIds = (await conn.QueryAsync<int>("SELECT id FROM tourists ORDER BY id")).ToArray();
        var transportIds = (await conn.QueryAsync<int>("SELECT id FROM transport_types ORDER BY id")).ToArray();
        var tagIds = (await conn.QueryAsync<int>("SELECT id FROM tags ORDER BY id")).ToArray();

        await CheckConsistencyAsync(sqlMulti, sqlJson, mongo, activeIds, countries, categoryIds);

        var detailIds = Enumerable.Range(0, N(3000)).Select(_ => activeIds[_rnd.Next(activeIds.Length)]).ToArray();
        await Compare("Деталі туру за id", detailIds, 300,
            (sqlMulti.Name, id => sqlMulti.GetTourAsync(id)),
            (sqlJson.Name, id => sqlJson.GetTourAsync(id)),
            (mongo.Name, id => mongo.GetTourAsync(id)));

        var parallelIds = Enumerable.Range(0, N(16000)).Select(_ => activeIds[_rnd.Next(activeIds.Length)]).ToArray();
        await CompareParallel("Деталі туру за id, 16 паралельних клієнтів", parallelIds, 16,
            (sqlMulti.Name, id => sqlMulti.GetTourAsync(id)),
            (sqlJson.Name, id => sqlJson.GetTourAsync(id)),
            (mongo.Name, id => mongo.GetTourAsync(id)));

        var criteria = Enumerable.Range(0, N(1000)).Select(_ =>
        {
            var min = _rnd.Next(0, 600) * 50m;
            return new SearchCriteria(countries[_rnd.Next(countries.Length)], categoryIds[_rnd.Next(categoryIds.Length)], min, min + _rnd.Next(100, 900) * 50m);
        }).ToArray();
        await Compare("Пошук у каталозі (країна + категорія + ціна, сортування за рейтингом)", criteria, 100,
            ("PostgreSQL", c => sqlMulti.SearchAsync(c)),
            ("MongoDB", c => mongo.SearchAsync(c)));

        var statsRuns = Enumerable.Range(0, N(40)).ToArray();
        await Compare("Аналітика: кількість турів і середня ціна по країнах", statsRuns, 5,
            ("PostgreSQL", _ => sqlMulti.CountryStatsAsync()),
            ("MongoDB", _ => mongo.CountryStatsAsync()));

        var newTours = Enumerable.Range(0, N(1000)).Select(i => MakeTour(i, categoryIds, guideIds, locationIds, hotelIds, transportIds, tagIds)).ToArray();
        await Compare("Створення туру з маршрутом, готелями, атрибутами (запис)", newTours, 50,
            ("PostgreSQL", t => sqlMulti.CreateTourAsync(t)),
            ("MongoDB", t => mongo.CreateTourAsync(t)));

        var reviews = Enumerable.Range(0, N(2000)).Select(_ => new NewReview(
            activeIds[_rnd.Next(activeIds.Length)], touristIds[_rnd.Next(touristIds.Length)], _rnd.Next(1, 6),
            Catalog.ReviewComments[_rnd.Next(Catalog.ReviewComments.Length)],
            new DateTime(2026, 11, 2, 0, 0, 0).AddSeconds(_rnd.Next(0, 864000)))).ToArray();
        await Compare("Додавання відгуку (запис + підтримка рейтингу)", reviews, 100,
            ("PostgreSQL", r => sqlMulti.AddReviewAsync(r)),
            ("MongoDB", r => mongo.AddReviewAsync(r)));

        var renames = Enumerable.Range(0, N(300)).Select(i => (Id: hotelIds[_rnd.Next(hotelIds.Length)], Name: $"Готель Ренейм {i}")).ToArray();
        await Compare("Перейменування готелю (оновлення довідника)", renames, 20,
            ("PostgreSQL", h => sqlMulti.RenameHotelAsync(h.Id, h.Name)),
            ("MongoDB", h => mongo.RenameHotelAsync(h.Id, h.Name)));

        await CheckConsistencyAfterWritesAsync(sqlMulti, mongo, reviews, renames);
        await CollectStorageAsync();
        return _report;
    }

    private NewTour MakeTour(int i, int[] cats, int[] guides, int[] locs, int[] hotels, int[] transports, int[] tags)
    {
        var category = cats[_rnd.Next(cats.Length)];
        var attributes = new Dictionary<string, object>
        {
            ["difficulty"] = _rnd.Next(1, 6),
            ["all_inclusive"] = _rnd.Next(2) == 1,
            ["meals"] = "сніданки",
            ["languages"] = "uk,en",
            ["min_age"] = _rnd.Next(0, 18)
        };
        return new NewTour
        {
            Title = $"Новий тур {i}",
            Description = "Тур, створений під час тестування швидкодії.",
            Price = _rnd.Next(20, 1600) * 50m,
            DurationDays = _rnd.Next(2, 15),
            CategoryId = category,
            GuideId = guides[_rnd.Next(guides.Length)],
            UpdatedBy = 1,
            LocationIds = locs.OrderBy(_ => _rnd.Next()).Take(_rnd.Next(2, 7)).ToList(),
            Hotels = hotels.OrderBy(_ => _rnd.Next()).Take(_rnd.Next(1, 4)).Select(h => new HotelNights(h, _rnd.Next(1, 6))).ToList(),
            TransportIds = transports.OrderBy(_ => _rnd.Next()).Take(_rnd.Next(1, 4)).ToList(),
            Attributes = attributes,
            TagIds = tags.OrderBy(_ => _rnd.Next()).Take(_rnd.Next(2, 6)).ToList()
        };
    }

    private async Task Compare<T>(string scenario, T[] inputs, int warmup, params (string Name, Func<T, Task> Op)[] engines)
    {
        Console.WriteLine($"\n=== {scenario} ({inputs.Length} операцій)");
        foreach (var (name, op) in engines)
        {
            for (var i = 0; i < Math.Min(warmup, inputs.Length); i++) await op(inputs[i]);

            var samples = new List<double>(inputs.Length);
            var wall = Stopwatch.StartNew();
            foreach (var input in inputs)
            {
                var start = Stopwatch.GetTimestamp();
                await op(input);
                samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            wall.Stop();
            Add(Measurement.From(scenario, name, samples, wall.Elapsed.TotalSeconds, 1));
        }
    }

    private async Task CompareParallel<T>(string scenario, T[] inputs, int workers, params (string Name, Func<T, Task> Op)[] engines)
    {
        Console.WriteLine($"\n=== {scenario} ({inputs.Length} операцій)");
        foreach (var (name, op) in engines)
        {
            for (var i = 0; i < Math.Min(500, inputs.Length); i++) await op(inputs[i]);

            var samples = new List<double>[workers];
            var next = -1;
            var wall = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, workers).Select(async w =>
            {
                samples[w] = new List<double>();
                int idx;
                while ((idx = Interlocked.Increment(ref next)) < inputs.Length)
                {
                    var start = Stopwatch.GetTimestamp();
                    await op(inputs[idx]);
                    samples[w].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
            }));
            wall.Stop();
            Add(Measurement.From(scenario, name, samples.SelectMany(s => s).ToList(), wall.Elapsed.TotalSeconds, workers));
        }
    }

    private void Add(Measurement m)
    {
        _report.Results.Add(m);
        Console.WriteLine($"  {m.Engine,-30} mean {m.MeanMs,8:F3} ms | p50 {m.MedianMs,8:F3} | p95 {m.P95Ms,8:F3} | p99 {m.P99Ms,8:F3} | {m.OpsPerSecond,9:F0} op/s");
    }

    private async Task CheckConsistencyAsync(ITourCatalogRepository sqlMulti, ITourCatalogRepository sqlJson, ITourCatalogRepository mongo,
        int[] activeIds, string[] countries, int[] categoryIds)
    {
        Console.WriteLine("Перевірка еквівалентності даних у PostgreSQL і MongoDB...");
        var ids = Enumerable.Range(0, 300).Select(_ => activeIds[_rnd.Next(activeIds.Length)]).ToArray();
        var mismatches = 0;
        foreach (var id in ids)
        {
            var a = Fingerprint(await sqlMulti.GetTourAsync(id));
            var b = Fingerprint(await sqlJson.GetTourAsync(id));
            var c = Fingerprint(await mongo.GetTourAsync(id));
            if (a != b || a != c)
            {
                mismatches++;
                if (mismatches <= 3) Console.WriteLine($"  розбіжність у турі {id}:\n    {a}\n    {b}\n    {c}");
            }
        }
        Note($"Деталі туру: перевірено {ids.Length} випадкових турів, розбіжностей: {mismatches}");

        var searchMismatch = 0;
        for (var i = 0; i < 200; i++)
        {
            var min = _rnd.Next(0, 600) * 50m;
            var c = new SearchCriteria(countries[_rnd.Next(countries.Length)], categoryIds[_rnd.Next(categoryIds.Length)], min, min + _rnd.Next(100, 900) * 50m);
            var a = string.Join(",", (await sqlMulti.SearchAsync(c)).Select(x => x.Id));
            var b = string.Join(",", (await mongo.SearchAsync(c)).Select(x => x.Id));
            if (a != b) searchMismatch++;
        }
        Note($"Пошук: перевірено 200 випадкових запитів, розбіжностей у результатах: {searchMismatch}");

        var s1 = JsonSerializer.Serialize(await sqlMulti.CountryStatsAsync());
        var s2 = JsonSerializer.Serialize(await mongo.CountryStatsAsync());
        Note($"Аналітика по країнах: результати {(s1 == s2 ? "співпадають" : "НЕ співпадають")}");
    }

    private async Task CheckConsistencyAfterWritesAsync(ITourCatalogRepository sql, ITourCatalogRepository mongo, NewReview[] reviews, (int Id, string Name)[] renames)
    {
        var touched = reviews.Select(r => r.TourId).Distinct().Take(200).ToArray();
        var mismatches = 0;
        foreach (var id in touched)
        {
            var a = Fingerprint(await sql.GetTourAsync(id));
            var b = Fingerprint(await mongo.GetTourAsync(id));
            if (a != b) mismatches++;
        }
        Note($"Після записів: перевірено {touched.Length} турів з новими відгуками/перейменованими готелями, розбіжностей: {mismatches}");
    }

    private void Note(string text)
    {
        Console.WriteLine("  " + text);
        _report.ConsistencyChecks.Add(text);
    }

    private static string Fingerprint(TourDetails? t)
    {
        if (t is null) return "null";
        return string.Join("|",
            t.Id, t.Title, t.Price.ToString("F2"), t.DurationDays, t.Category.Id, t.Category.Name, t.Guide.Id, t.Guide.Name,
            string.Join(",", t.Route.Select(r => $"{r.Order}:{r.Location.Id}:{r.Location.Country}")),
            string.Join(",", t.Hotels.Select(h => $"{h.Id}:{h.Name}:{h.Nights}:{h.Location.Id}")),
            string.Join(",", t.Transports.Select(x => x.Id)),
            string.Join(",", t.Attributes.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}")),
            string.Join(",", t.Tags),
            t.AvgRating.ToString("F6"), t.ReviewsCount,
            string.Join(",", t.LatestReviews.Select(r => $"{r.Id}:{r.Author}:{r.Rating}:{r.CreatedAt:yyyyMMddHHmmss}")));
    }

    private async Task CollectEnvironmentAsync()
    {
        await using var conn = await _pg.OpenConnectionAsync();
        _report.Environment["PostgreSQL"] = await conn.ExecuteScalarAsync<string>("SHOW server_version") ?? "";
        var build = await _mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("buildInfo", 1));
        _report.Environment["MongoDB"] = build["version"].AsString;
        _report.Environment[".NET"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        _report.Environment["OS"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        _report.Environment["CPU"] = ReadCpuName();
        _report.Environment["Логічних ядер"] = System.Environment.ProcessorCount.ToString();
        _report.Environment["Розгортання"] = "Docker (postgres:17, mongo:8.0), той самий хост";
        _report.Environment["Гарантія запису"] = "PostgreSQL synchronous_commit=on, MongoDB w:1 j:true";

        foreach (var table in new[] { "tours", "locations", "hotels", "tour_locations", "tour_hotels", "tour_transports", "tour_attributes", "tour_tags", "reviews", "tourists", "bookings", "payments" })
            _report.DatasetSize[table] = await conn.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}");
    }

    private async Task CollectStorageAsync()
    {
        await using var conn = await _pg.OpenConnectionAsync();
        await conn.ExecuteAsync("VACUUM ANALYZE");
        var catalogTables = new[] { "tours", "tour_categories", "guides", "users", "locations", "tour_locations", "hotels", "tour_hotels",
            "transport_types", "tour_transports", "tour_attributes", "tags", "tour_tags", "reviews", "tourists" };
        foreach (var t in catalogTables)
        {
            var row = await conn.QuerySingleAsync<(long rows, long data, long idx)>($"""
                SELECT (SELECT COUNT(*) FROM {t}), pg_table_size('{t}'), pg_indexes_size('{t}')
                """);
            _report.Storage.Add(new StorageInfo { Engine = "PostgreSQL", Object = t, Rows = row.rows, DataBytes = row.data, IndexBytes = row.idx });
        }

        foreach (var c in new[] { "tours", "reviews", "hotels", "locations", "tourists", "guides", "categories", "transport_types", "tags" })
        {
            var stats = await _mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("collStats", c));
            _report.Storage.Add(new StorageInfo
            {
                Engine = "MongoDB",
                Object = c,
                Rows = stats["count"].ToInt64(),
                DataBytes = stats["storageSize"].ToInt64(),
                IndexBytes = stats["totalIndexSize"].ToInt64()
            });
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"));
            return line?.Split(':', 2)[1].Trim() ?? "невідомо";
        }
        catch
        {
            return "невідомо";
        }
    }
}

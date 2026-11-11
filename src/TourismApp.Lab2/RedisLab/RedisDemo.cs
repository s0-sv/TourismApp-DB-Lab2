using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using StackExchange.Redis;
using TourismApp.Lab2.Repositories;

namespace TourismApp.Lab2.RedisLab;

public class RedisDemo
{
    private readonly IDatabase _db;
    private readonly ConnectionMultiplexer _mux;
    private readonly NpgsqlDataSource _pg;
    private readonly List<string> _log = new();

    public RedisDemo(ConnectionMultiplexer mux, NpgsqlDataSource pg)
    {
        _mux = mux;
        _db = mux.GetDatabase();
        _pg = pg;
    }

    public async Task<List<string>> RunAsync()
    {
        foreach (var old in _mux.GetServer(_mux.GetEndPoints()[0]).Keys(pattern: "lab2:*"))
            await _db.KeyDeleteAsync(old);

        Section("1. Рядки (STRING) + TTL: сесія користувача");
        await _db.StringSetAsync("lab2:session:abc123", "user:42", TimeSpan.FromMinutes(30));
        Log("SET lab2:session:abc123 user:42 EX 1800", "OK");
        Log("GET lab2:session:abc123", (string?)await _db.StringGetAsync("lab2:session:abc123"));
        Log("TTL lab2:session:abc123", ((int)(await _db.KeyTimeToLiveAsync("lab2:session:abc123"))!.Value.TotalSeconds).ToString());
        Log("SET lab2:promo:SUMMER -10% NX", (await _db.StringSetAsync("lab2:promo:SUMMER", "-10%", when: When.NotExists)).ToString());
        Log("SET lab2:promo:SUMMER -20% NX (повторно)", (await _db.StringSetAsync("lab2:promo:SUMMER", "-20%", when: When.NotExists)).ToString());

        Section("2. Лічильники: перегляди туру");
        for (var i = 0; i < 5; i++) await _db.StringIncrementAsync("lab2:tour:15:views");
        Log("INCR lab2:tour:15:views (x5)", (string?)await _db.StringGetAsync("lab2:tour:15:views"));
        Log("INCRBY lab2:tour:15:views 100", (await _db.StringIncrementAsync("lab2:tour:15:views", 100)).ToString());

        Section("3. Хеш (HASH): картка туру");
        await _db.HashSetAsync("lab2:tour:15", new HashEntry[]
        {
            new("title", "Романтичний Париж"), new("price", "25000"), new("category", "Екскурсійний"), new("free_seats", "12")
        });
        Log("HSET lab2:tour:15 title ... price 25000 category ... free_seats 12", "4");
        Log("HGET lab2:tour:15 price", (string?)await _db.HashGetAsync("lab2:tour:15", "price"));
        Log("HINCRBY lab2:tour:15 free_seats -2", (await _db.HashIncrementAsync("lab2:tour:15", "free_seats", -2)).ToString());
        Log("HGETALL lab2:tour:15", string.Join(", ", (await _db.HashGetAllAsync("lab2:tour:15")).Select(e => $"{e.Name}={e.Value}")));
        Log("HEXISTS lab2:tour:15 discount", (await _db.HashExistsAsync("lab2:tour:15", "discount")).ToString());

        Section("4. Список (LIST): нещодавно переглянуті тури");
        foreach (var id in new[] { 3, 15, 8, 42, 15, 7 })
        {
            await _db.ListRemoveAsync("lab2:user:42:recent", id);
            await _db.ListLeftPushAsync("lab2:user:42:recent", id);
            await _db.ListTrimAsync("lab2:user:42:recent", 0, 4);
        }
        Log("LREM + LPUSH + LTRIM 0 4 (перегляди 3,15,8,42,15,7)", "OK");
        Log("LRANGE lab2:user:42:recent 0 -1", string.Join(", ", await _db.ListRangeAsync("lab2:user:42:recent")));
        Log("LLEN lab2:user:42:recent", (await _db.ListLengthAsync("lab2:user:42:recent")).ToString());

        Section("5. Множина (SET): обрані тури");
        await _db.SetAddAsync("lab2:user:42:favorites", new RedisValue[] { 3, 15, 8, 21 });
        await _db.SetAddAsync("lab2:user:77:favorites", new RedisValue[] { 15, 21, 30 });
        Log("SADD lab2:user:42:favorites 3 15 8 21", "4");
        Log("SADD lab2:user:77:favorites 15 21 30", "3");
        Log("SISMEMBER lab2:user:42:favorites 8", (await _db.SetContainsAsync("lab2:user:42:favorites", 8)).ToString());
        Log("SINTER lab2:user:42:favorites lab2:user:77:favorites",
            string.Join(", ", (await _db.SetCombineAsync(SetOperation.Intersect, "lab2:user:42:favorites", "lab2:user:77:favorites")).Select(x => (int)x).Order()));
        Log("SUNION lab2:user:42:favorites lab2:user:77:favorites",
            string.Join(", ", (await _db.SetCombineAsync(SetOperation.Union, "lab2:user:42:favorites", "lab2:user:77:favorites")).Select(x => (int)x).Order()));
        Log("SREM lab2:user:42:favorites 3", (await _db.SetRemoveAsync("lab2:user:42:favorites", 3)).ToString());
        Log("SCARD lab2:user:42:favorites", (await _db.SetLengthAsync("lab2:user:42:favorites")).ToString());

        Section("6. Впорядкована множина (ZSET): рейтинг популярності турів");
        await _db.SortedSetAddAsync("lab2:top:bookings", new SortedSetEntry[]
        {
            new("Романтичний Париж", 120), new("Історичний Рим", 95), new("Похід на Говерлу", 140), new("Лижний тур в Карпати", 80)
        });
        Log("ZADD lab2:top:bookings 120 Париж 95 Рим 140 Говерла 80 Карпати", "4");
        Log("ZINCRBY lab2:top:bookings 30 \"Історичний Рим\"", (await _db.SortedSetIncrementAsync("lab2:top:bookings", "Історичний Рим", 30)).ToString());
        Log("ZREVRANGE lab2:top:bookings 0 2 WITHSCORES",
            string.Join(", ", (await _db.SortedSetRangeByRankWithScoresAsync("lab2:top:bookings", 0, 2, Order.Descending)).Select(e => $"{e.Element}={e.Score}")));
        Log("ZREVRANK lab2:top:bookings \"Лижний тур в Карпати\"", (await _db.SortedSetRankAsync("lab2:top:bookings", "Лижний тур в Карпати", Order.Descending)).ToString());
        Log("ZRANGEBYSCORE lab2:top:bookings 100 +inf",
            string.Join(", ", await _db.SortedSetRangeByScoreAsync("lab2:top:bookings", 100, double.PositiveInfinity)));

        Section("7. Транзакція (MULTI/EXEC + WATCH): бронювання місця");
        var tx = _db.CreateTransaction();
        tx.AddCondition(Condition.HashNotEqual("lab2:tour:15", "free_seats", 0));
        _ = tx.HashIncrementAsync("lab2:tour:15", "free_seats", -1);
        _ = tx.ListRightPushAsync("lab2:tour:15:bookings", "user:42");
        Log("WATCH + MULTI; HINCRBY free_seats -1; RPUSH bookings user:42; EXEC", (await tx.ExecuteAsync()).ToString());
        Log("HGET lab2:tour:15 free_seats", (string?)await _db.HashGetAsync("lab2:tour:15", "free_seats"));

        Section("8. Pub/Sub: сповіщення про зміну ціни");
        var received = new TaskCompletionSource<string>();
        var sub = _mux.GetSubscriber();
        var channel = RedisChannel.Literal("lab2:tour-updates");
        await sub.SubscribeAsync(channel, (_, msg) => received.TrySetResult(msg!));
        var receivers = await sub.PublishAsync(channel, "tour:15 price=23000");
        Log("SUBSCRIBE lab2:tour-updates; PUBLISH lab2:tour-updates \"tour:15 price=23000\"", $"отримувачів: {receivers}");
        Log("Повідомлення, отримане підписником", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await sub.UnsubscribeAsync(channel);

        Section("9. Кеш (cache-aside) для деталей туру з PostgreSQL");
        var repo = new SqlTourCatalogRepository(_pg, SqlReadMode.MultipleQueries);
        const int tourId = 15;
        var key = $"lab2:cache:tour:{tourId}";
        var sw = Stopwatch.StartNew();
        var cached = await _db.StringGetAsync(key);
        if (cached.IsNull)
        {
            var details = await repo.GetTourAsync(tourId);
            await _db.StringSetAsync(key, JsonSerializer.Serialize(details), TimeSpan.FromMinutes(5));
        }
        var miss = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var hit = await _db.StringGetAsync(key);
        var hitTime = sw.Elapsed.TotalMilliseconds;
        var hitDetails = JsonSerializer.Deserialize<Domain.TourDetails>(hit.ToString())!;
        Log("GET (промах) -> SQL -> SET EX 300", $"{miss:F3} мс");
        Log("GET (влучання)", $"{hitTime:F3} мс, тур \"{hitDetails.Title}\"");

        Section("10. Робота з ключами");
        Log("EXISTS lab2:tour:15", (await _db.KeyExistsAsync("lab2:tour:15")).ToString());
        Log("TYPE lab2:top:bookings", (await _db.KeyTypeAsync("lab2:top:bookings")).ToString());
        Log("EXPIRE lab2:tour:15:views 60", (await _db.KeyExpireAsync("lab2:tour:15:views", TimeSpan.FromSeconds(60))).ToString());
        Log("DEL lab2:promo:SUMMER", (await _db.KeyDeleteAsync("lab2:promo:SUMMER")).ToString());
        var keys = _mux.GetServer(_mux.GetEndPoints()[0]).Keys(pattern: "lab2:*").Select(k => k.ToString()).Order().ToList();
        Log("SCAN 0 MATCH lab2:*", string.Join(", ", keys));

        return _log;
    }

    private void Section(string title)
    {
        Console.WriteLine($"\n--- {title}");
        _log.Add($"## {title}");
    }

    private void Log(string command, string? result)
    {
        Console.WriteLine($"  > {command}\n    {result}");
        _log.Add($"> {command}\n{result}");
    }
}

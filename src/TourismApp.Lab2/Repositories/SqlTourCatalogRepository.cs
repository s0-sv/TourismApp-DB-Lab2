using System.Text.Json;
using Dapper;
using Npgsql;
using TourismApp.Lab2.Domain;

namespace TourismApp.Lab2.Repositories;

public interface ITourCatalogRepository
{
    string Name { get; }
    Task<TourDetails?> GetTourAsync(int id);
    Task<IReadOnlyList<TourSummary>> SearchAsync(SearchCriteria criteria);
    Task<int> CreateTourAsync(NewTour tour);
    Task AddReviewAsync(NewReview review);
    Task<IReadOnlyList<CountryStat>> CountryStatsAsync();
    Task RenameHotelAsync(int hotelId, string newName);
}

public enum SqlReadMode
{
    MultipleQueries,
    JsonAggregation
}

public class SqlTourCatalogRepository : ITourCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly NpgsqlDataSource _dataSource;
    private readonly SqlReadMode _mode;

    static SqlTourCatalogRepository()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public SqlTourCatalogRepository(NpgsqlDataSource dataSource, SqlReadMode mode = SqlReadMode.MultipleQueries)
    {
        _dataSource = dataSource;
        _mode = mode;
    }

    public string Name => _mode == SqlReadMode.MultipleQueries ? "PostgreSQL (8 SELECT + JOIN)" : "PostgreSQL (json_agg)";

    public Task<TourDetails?> GetTourAsync(int id) =>
        _mode == SqlReadMode.MultipleQueries ? GetTourMultipleAsync(id) : GetTourJsonAsync(id);

    private const string DetailsBatchSql = """
        SELECT t.id, t.title, t.description, t.price, t.duration_days,
               c.id AS category_id, c.name AS category_name,
               g.id AS guide_id, u.username AS guide_name, g.bio AS guide_bio, g.rating AS guide_rating
        FROM tours t
        JOIN tour_categories c ON c.id = t.category_id
        JOIN guides g ON g.id = t.guide_id
        JOIN users u ON u.id = g.user_id
        WHERE t.id = @id AND t.is_deleted = FALSE;

        SELECT tl.visit_order AS "order", l.id, l.name, l.country, l.city
        FROM tour_locations tl
        JOIN locations l ON l.id = tl.location_id
        WHERE tl.tour_id = @id
        ORDER BY tl.visit_order;

        SELECT h.id, h.name, h.star_rating AS stars, th.nights_count AS nights,
               l.id AS location_id, l.name AS location_name, l.country, l.city
        FROM tour_hotels th
        JOIN hotels h ON h.id = th.hotel_id
        JOIN locations l ON l.id = h.location_id
        WHERE th.tour_id = @id
        ORDER BY th.id;

        SELECT tt.id AS link_id, tp.id, tp.name
        FROM tour_transports tt
        JOIN transport_types tp ON tp.id = tt.transport_type_id
        WHERE tt.tour_id = @id
        ORDER BY tt.id;

        SELECT attr_key, attr_value FROM tour_attributes WHERE tour_id = @id;

        SELECT tg.name
        FROM tour_tags tt
        JOIN tags tg ON tg.id = tt.tag_id
        WHERE tt.tour_id = @id
        ORDER BY tg.name COLLATE "C";

        SELECT COALESCE(AVG(rating), 0)::float8 AS avg_rating, COUNT(*)::int AS reviews_count
        FROM reviews WHERE tour_id = @id;

        SELECT r.id, tr.first_name || ' ' || tr.last_name AS author, r.rating, r.comment, r.created_at
        FROM reviews r
        JOIN tourists tr ON tr.id = r.tourist_id
        WHERE r.tour_id = @id
        ORDER BY r.created_at DESC, r.id DESC
        LIMIT 5;
        """;

    private async Task<TourDetails?> GetTourMultipleAsync(int id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var grid = await conn.QueryMultipleAsync(DetailsBatchSql, new { id });

        var head = await grid.ReadSingleOrDefaultAsync<TourHeadRow>();
        if (head is null) return null;

        var route = (await grid.ReadAsync<RouteRow>())
            .Select(r => new RouteStop { Order = r.Order, Location = new LocationInfo { Id = r.Id, Name = r.Name, Country = r.Country, City = r.City } })
            .ToList();
        var hotels = (await grid.ReadAsync<HotelRow>())
            .Select(h => new HotelStay
            {
                Id = h.Id, Name = h.Name, Stars = h.Stars, Nights = h.Nights,
                Location = new LocationInfo { Id = h.LocationId, Name = h.LocationName, Country = h.Country, City = h.City }
            })
            .ToList();
        var transports = (await grid.ReadAsync<TransportRow>())
            .Select(t => new TransportInfo { Id = t.Id, Name = t.Name })
            .ToList();
        var attributes = (await grid.ReadAsync<AttributeRow>()).ToDictionary(a => a.AttrKey, a => a.AttrValue);
        var tags = (await grid.ReadAsync<string>()).ToList();
        var rating = await grid.ReadSingleAsync<RatingRow>();
        var latest = (await grid.ReadAsync<ReviewInfo>()).ToList();

        return new TourDetails
        {
            Id = head.Id,
            Title = head.Title,
            Description = head.Description,
            Price = head.Price,
            DurationDays = head.DurationDays,
            Category = new CategoryInfo { Id = head.CategoryId, Name = head.CategoryName },
            Guide = new GuideInfo { Id = head.GuideId, Name = head.GuideName, Bio = head.GuideBio, Rating = head.GuideRating },
            Route = route,
            Hotels = hotels,
            Transports = transports,
            Attributes = attributes,
            Tags = tags,
            AvgRating = rating.AvgRating,
            ReviewsCount = rating.ReviewsCount,
            LatestReviews = latest
        };
    }

    private const string DetailsJsonSql = """
        SELECT json_build_object(
            'id', t.id,
            'title', t.title,
            'description', t.description,
            'price', t.price,
            'durationDays', t.duration_days,
            'category', json_build_object('id', c.id, 'name', c.name),
            'guide', json_build_object('id', g.id, 'name', u.username, 'bio', g.bio, 'rating', g.rating),
            'route', COALESCE((
                SELECT json_agg(json_build_object('order', tl.visit_order,
                       'location', json_build_object('id', l.id, 'name', l.name, 'country', l.country, 'city', l.city))
                       ORDER BY tl.visit_order)
                FROM tour_locations tl JOIN locations l ON l.id = tl.location_id
                WHERE tl.tour_id = t.id), '[]'::json),
            'hotels', COALESCE((
                SELECT json_agg(json_build_object('id', h.id, 'name', h.name, 'stars', h.star_rating, 'nights', th.nights_count,
                       'location', json_build_object('id', l.id, 'name', l.name, 'country', l.country, 'city', l.city))
                       ORDER BY th.id)
                FROM tour_hotels th JOIN hotels h ON h.id = th.hotel_id JOIN locations l ON l.id = h.location_id
                WHERE th.tour_id = t.id), '[]'::json),
            'transports', COALESCE((
                SELECT json_agg(json_build_object('id', tp.id, 'name', tp.name) ORDER BY tt.id)
                FROM tour_transports tt JOIN transport_types tp ON tp.id = tt.transport_type_id
                WHERE tt.tour_id = t.id), '[]'::json),
            'attributes', COALESCE((
                SELECT json_object_agg(a.attr_key, a.attr_value) FROM tour_attributes a WHERE a.tour_id = t.id), '{}'::json),
            'tags', COALESCE((
                SELECT json_agg(tg.name ORDER BY tg.name COLLATE "C")
                FROM tour_tags tt JOIN tags tg ON tg.id = tt.tag_id WHERE tt.tour_id = t.id), '[]'::json),
            'avgRating', rs.avg_rating,
            'reviewsCount', rs.reviews_count,
            'latestReviews', COALESCE((
                SELECT json_agg(x) FROM (
                    SELECT r.id, tr.first_name || ' ' || tr.last_name AS author, r.rating, r.comment, r.created_at AS "createdAt"
                    FROM reviews r JOIN tourists tr ON tr.id = r.tourist_id
                    WHERE r.tour_id = t.id
                    ORDER BY r.created_at DESC, r.id DESC
                    LIMIT 5) x), '[]'::json)
        )::text
        FROM tours t
        JOIN tour_categories c ON c.id = t.category_id
        JOIN guides g ON g.id = t.guide_id
        JOIN users u ON u.id = g.user_id
        CROSS JOIN LATERAL (
            SELECT COALESCE(AVG(rating), 0)::float8 AS avg_rating, COUNT(*)::int AS reviews_count
            FROM reviews WHERE tour_id = t.id) rs
        WHERE t.id = @id AND t.is_deleted = FALSE
        """;

    private async Task<TourDetails?> GetTourJsonAsync(int id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var json = await conn.QuerySingleOrDefaultAsync<string>(DetailsJsonSql, new { id });
        return json is null ? null : JsonSerializer.Deserialize<TourDetails>(json, JsonOptions);
    }

    private const string SearchSql = """
        SELECT t.id, t.title, t.price, c.name AS category, rs.avg_rating, rs.reviews_count
        FROM tours t
        JOIN tour_categories c ON c.id = t.category_id
        CROSS JOIN LATERAL (
            SELECT COALESCE(AVG(r.rating), 0)::float8 AS avg_rating, COUNT(*)::int AS reviews_count
            FROM reviews r WHERE r.tour_id = t.id) rs
        WHERE t.is_deleted = FALSE
          AND t.category_id = @CategoryId
          AND t.price BETWEEN @MinPrice AND @MaxPrice
          AND EXISTS (
              SELECT 1 FROM tour_locations tl JOIN locations l ON l.id = tl.location_id
              WHERE tl.tour_id = t.id AND l.country = @Country)
        ORDER BY rs.avg_rating DESC, t.id
        LIMIT @Limit
        """;

    public async Task<IReadOnlyList<TourSummary>> SearchAsync(SearchCriteria criteria)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<TourSummary>(SearchSql, criteria)).AsList();
    }

    public async Task<int> CreateTourAsync(NewTour tour)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var id = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO tours (title, description, price, duration_days, guide_id, category_id, updated_by)
            VALUES (@Title, @Description, @Price, @DurationDays, @GuideId, @CategoryId, @UpdatedBy)
            RETURNING id
            """, tour, tx);

        await conn.ExecuteAsync("""
            INSERT INTO tour_locations (tour_id, location_id, visit_order)
            SELECT @id, loc, ord FROM unnest(@locs) WITH ORDINALITY AS u(loc, ord)
            """, new { id, locs = tour.LocationIds.ToArray() }, tx);

        if (tour.Hotels.Count > 0)
            await conn.ExecuteAsync("""
                INSERT INTO tour_hotels (tour_id, hotel_id, nights_count)
                SELECT @id, h, n FROM unnest(@hotels, @nights) AS u(h, n)
                """, new { id, hotels = tour.Hotels.Select(h => h.HotelId).ToArray(), nights = tour.Hotels.Select(h => h.Nights).ToArray() }, tx);

        await conn.ExecuteAsync("""
            INSERT INTO tour_transports (tour_id, transport_type_id)
            SELECT @id, tr FROM unnest(@trs) AS u(tr)
            """, new { id, trs = tour.TransportIds.ToArray() }, tx);

        await conn.ExecuteAsync("""
            INSERT INTO tour_attributes (tour_id, attr_key, attr_value)
            SELECT @id, k, v FROM unnest(@keys, @vals) AS u(k, v)
            """, new
        {
            id,
            keys = tour.Attributes.Keys.ToArray(),
            vals = tour.Attributes.Values.Select(AttributeFormat.ToText).ToArray()
        }, tx);

        await conn.ExecuteAsync("""
            INSERT INTO tour_tags (tour_id, tag_id)
            SELECT @id, tg FROM unnest(@tags) AS u(tg)
            """, new { id, tags = tour.TagIds.ToArray() }, tx);

        await tx.CommitAsync();
        return id;
    }

    public async Task AddReviewAsync(NewReview review)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("""
            INSERT INTO reviews (tour_id, tourist_id, rating, comment, created_at)
            VALUES (@TourId, @TouristId, @Rating, @Comment, @CreatedAt)
            """, new
        {
            review.TourId, review.TouristId, review.Rating, review.Comment,
            CreatedAt = DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Unspecified)
        });
    }

    private const string CountryStatsSql = """
        SELECT country, COUNT(*)::int AS tours_count, ROUND(AVG(price), 2) AS avg_price
        FROM (
            SELECT DISTINCT l.country, t.id, t.price
            FROM tours t
            JOIN tour_locations tl ON tl.tour_id = t.id
            JOIN locations l ON l.id = tl.location_id
            WHERE t.is_deleted = FALSE
        ) x
        GROUP BY country
        ORDER BY tours_count DESC, country COLLATE "C"
        """;

    public async Task<IReadOnlyList<CountryStat>> CountryStatsAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        return (await conn.QueryAsync<CountryStat>(CountryStatsSql)).AsList();
    }

    public async Task RenameHotelAsync(int hotelId, string newName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("UPDATE hotels SET name = @newName WHERE id = @hotelId", new { hotelId, newName });
    }

    private class TourHeadRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int GuideId { get; set; }
        public string GuideName { get; set; } = "";
        public string? GuideBio { get; set; }
        public decimal GuideRating { get; set; }
    }

    private class RouteRow
    {
        public int Order { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public string? City { get; set; }
    }

    private class HotelRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Stars { get; set; }
        public int Nights { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = "";
        public string Country { get; set; } = "";
        public string? City { get; set; }
    }

    private class TransportRow
    {
        public int LinkId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private class AttributeRow
    {
        public string AttrKey { get; set; } = "";
        public string AttrValue { get; set; } = "";
    }

    private class RatingRow
    {
        public double AvgRating { get; set; }
        public int ReviewsCount { get; set; }
    }
}

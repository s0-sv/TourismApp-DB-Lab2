using Npgsql;
using NpgsqlTypes;
using TourismApp.Lab2.Domain;

namespace TourismApp.Lab2.Data;

public class PostgresSeeder
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSeeder(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task SeedAsync(Dataset d)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        await Exec(conn, """
            TRUNCATE roles, users, tourists, guides, tour_categories, tours, locations, tour_locations,
                     bookings, payments, reviews, hotels, tour_hotels, transport_types, tour_transports,
                     tour_attributes, tags, tour_tags RESTART IDENTITY CASCADE
            """);

        await Copy(conn, "roles (id, name)", d.Roles, (w, r) => { w.Write(r.Id); w.Write(r.Name); });
        await Copy(conn, "users (id, username, email, password_hash, role_id, created_at)", d.Users, (w, u) =>
        {
            w.Write(u.Id); w.Write(u.Username); w.Write(u.Email); w.Write(u.PasswordHash); w.Write(u.RoleId);
            w.Write(u.CreatedAt, NpgsqlDbType.Timestamp);
        });
        await Copy(conn, "tourists (id, user_id, first_name, last_name, phone)", d.Tourists, (w, t) =>
        {
            w.Write(t.Id); w.Write(t.UserId); w.Write(t.FirstName); w.Write(t.LastName); w.Write(t.Phone);
        });
        await Copy(conn, "guides (id, user_id, bio, rating)", d.Guides, (w, g) =>
        {
            w.Write(g.Id); w.Write(g.UserId); w.Write(g.Bio); w.Write(g.Rating, NpgsqlDbType.Numeric);
        });
        await Copy(conn, "tour_categories (id, name)", d.Categories, (w, c) => { w.Write(c.Id); w.Write(c.Name); });
        await Copy(conn, "transport_types (id, name)", d.TransportTypes, (w, c) => { w.Write(c.Id); w.Write(c.Name); });
        await Copy(conn, "tags (id, name)", d.Tags, (w, c) => { w.Write(c.Id); w.Write(c.Name); });
        await Copy(conn, "locations (id, name, country, city)", d.Locations, (w, l) =>
        {
            w.Write(l.Id); w.Write(l.Name); w.Write(l.Country); w.Write(l.City);
        });
        await Copy(conn, "hotels (id, location_id, name, star_rating)", d.Hotels, (w, h) =>
        {
            w.Write(h.Id); w.Write(h.LocationId); w.Write(h.Name); w.Write(h.Stars);
        });
        await Copy(conn, "tours (id, title, description, price, duration_days, guide_id, category_id, is_deleted, updated_at, updated_by)", d.Tours, (w, t) =>
        {
            w.Write(t.Id); w.Write(t.Title); w.Write(t.Description); w.Write(t.Price, NpgsqlDbType.Numeric);
            w.Write(t.DurationDays); w.Write(t.GuideId); w.Write(t.CategoryId); w.Write(t.IsDeleted);
            w.Write(t.UpdatedAt, NpgsqlDbType.Timestamp); w.Write(t.UpdatedBy);
        });
        await Copy(conn, "tour_locations (id, tour_id, location_id, visit_order)", d.TourLocations, (w, r) =>
        {
            w.Write(r.Id); w.Write(r.TourId); w.Write(r.LocationId); w.Write(r.VisitOrder);
        });
        await Copy(conn, "tour_hotels (id, tour_id, hotel_id, nights_count)", d.TourHotels, (w, r) =>
        {
            w.Write(r.Id); w.Write(r.TourId); w.Write(r.HotelId); w.Write(r.Nights);
        });
        await Copy(conn, "tour_transports (id, tour_id, transport_type_id)", d.TourTransports, (w, r) =>
        {
            w.Write(r.Id); w.Write(r.TourId); w.Write(r.TransportTypeId);
        });
        await Copy(conn, "tour_attributes (id, tour_id, attr_key, attr_value)", d.TourAttributes, (w, r) =>
        {
            w.Write(r.Id); w.Write(r.TourId); w.Write(r.Key); w.Write(AttributeFormat.ToText(r.Value));
        });
        await Copy(conn, "tour_tags (tour_id, tag_id)", d.TourTags, (w, r) => { w.Write(r.TourId); w.Write(r.TagId); });
        await Copy(conn, "reviews (id, tour_id, tourist_id, rating, comment, created_at)", d.Reviews, (w, r) =>
        {
            w.Write(r.Id); w.Write(r.TourId); w.Write(r.TouristId); w.Write(r.Rating); w.Write(r.Comment);
            w.Write(r.CreatedAt, NpgsqlDbType.Timestamp);
        });
        await Copy(conn, "bookings (id, tour_id, tourist_id, booking_date, status, updated_by)", d.Bookings, (w, b) =>
        {
            w.Write(b.Id); w.Write(b.TourId); w.Write(b.TouristId); w.Write(b.BookingDate, NpgsqlDbType.Timestamp);
            w.Write(b.Status); w.Write(1);
        });
        await Copy(conn, "payments (id, booking_id, amount, payment_date, status)", d.Payments, (w, p) =>
        {
            w.Write(p.Id); w.Write(p.BookingId); w.Write(p.Amount, NpgsqlDbType.Numeric);
            w.Write(p.PaymentDate, NpgsqlDbType.Timestamp); w.Write(p.Status);
        });

        foreach (var table in new[] { "roles", "users", "tourists", "guides", "tour_categories", "transport_types", "tags",
                     "locations", "hotels", "tours", "tour_locations", "tour_hotels", "tour_transports",
                     "tour_attributes", "reviews", "bookings", "payments" })
        {
            await Exec(conn, $"SELECT setval(pg_get_serial_sequence('{table}', 'id'), COALESCE((SELECT MAX(id) FROM {table}), 0) + 1, false)");
        }

        await Exec(conn, "VACUUM ANALYZE");
    }

    private static async Task Exec(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 600 };
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task Copy<T>(NpgsqlConnection conn, string target, IEnumerable<T> rows, Action<NpgsqlBinaryImporter, T> write)
    {
        await using var importer = await conn.BeginBinaryImportAsync($"COPY {target} FROM STDIN (FORMAT BINARY)");
        foreach (var row in rows)
        {
            await importer.StartRowAsync();
            write(importer, row);
        }
        await importer.CompleteAsync();
    }
}

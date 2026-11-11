using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using TourismApp.Lab2.Domain;

namespace TourismApp.Lab2.Mongo;

public class RatingSummary
{
    public int Count { get; set; }
    public int Sum { get; set; }
    public double Avg { get; set; }
}

public class TourDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public bool IsDeleted { get; set; }
    public CategoryInfo Category { get; set; } = new();
    public GuideInfo Guide { get; set; } = new();
    public List<RouteStop> Route { get; set; } = new();
    public List<HotelStay> Hotels { get; set; } = new();
    public List<TransportInfo> Transports { get; set; } = new();
    public BsonDocument Attributes { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public RatingSummary Rating { get; set; } = new();
    public List<ReviewInfo> LatestReviews { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
    public int UpdatedBy { get; set; }
}

public class ReviewDocument
{
    [BsonId]
    public int Id { get; set; }
    public int TourId { get; set; }
    public int TouristId { get; set; }
    public string Author { get; set; } = "";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HotelDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Stars { get; set; }
    public LocationInfo Location { get; set; } = new();
}

public class TouristDocument
{
    [BsonId]
    public int Id { get; set; }
    public string FullName { get; set; } = "";
}

public class NamedDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class MongoContext
{
    private static int _conventionsRegistered;

    public IMongoDatabase Database { get; }
    public IMongoCollection<TourDocument> Tours => Database.GetCollection<TourDocument>("tours");
    public IMongoCollection<ReviewDocument> Reviews => Database.GetCollection<ReviewDocument>("reviews");
    public IMongoCollection<HotelDocument> Hotels => Database.GetCollection<HotelDocument>("hotels");
    public IMongoCollection<LocationInfo> Locations => Database.GetCollection<LocationInfo>("locations");
    public IMongoCollection<TouristDocument> Tourists => Database.GetCollection<TouristDocument>("tourists");
    public IMongoCollection<GuideInfo> Guides => Database.GetCollection<GuideInfo>("guides");
    public IMongoCollection<NamedDocument> Categories => Database.GetCollection<NamedDocument>("categories");
    public IMongoCollection<NamedDocument> TransportTypes => Database.GetCollection<NamedDocument>("transport_types");
    public IMongoCollection<NamedDocument> Tags => Database.GetCollection<NamedDocument>("tags");

    public MongoContext(string connectionString, string database)
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 0)
        {
            var pack = new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("lab2", pack, _ => true);
        }
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.WriteConcern = new WriteConcern(1, journal: true);
        Database = new MongoClient(settings).GetDatabase(database);
    }

    public async Task CreateIndexesAsync()
    {
        await Tours.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TourDocument>(
                Builders<TourDocument>.IndexKeys.Ascending("category._id").Ascending("route.location.country").Ascending("price"),
                new CreateIndexOptions<TourDocument>
                {
                    Name = "ix_search",
                    PartialFilterExpression = Builders<TourDocument>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TourDocument>(
                Builders<TourDocument>.IndexKeys.Ascending("hotels._id"),
                new CreateIndexOptions { Name = "ix_hotels" }),
            new CreateIndexModel<TourDocument>(
                Builders<TourDocument>.IndexKeys.Ascending("tags"),
                new CreateIndexOptions { Name = "ix_tags" })
        });
        await Reviews.Indexes.CreateOneAsync(new CreateIndexModel<ReviewDocument>(
            Builders<ReviewDocument>.IndexKeys.Ascending(x => x.TourId).Descending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "ix_tour_created" }));
    }
}

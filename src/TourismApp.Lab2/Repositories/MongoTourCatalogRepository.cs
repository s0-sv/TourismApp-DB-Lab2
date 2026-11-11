using MongoDB.Bson;
using MongoDB.Driver;
using TourismApp.Lab2.Data;
using TourismApp.Lab2.Domain;
using TourismApp.Lab2.Mongo;

namespace TourismApp.Lab2.Repositories;

public class MongoReferenceCache
{
    public Dictionary<int, LocationInfo> Locations { get; private set; } = new();
    public Dictionary<int, HotelDocument> Hotels { get; private set; } = new();
    public Dictionary<int, string> Categories { get; private set; } = new();
    public Dictionary<int, string> Transports { get; private set; } = new();
    public Dictionary<int, string> Tags { get; private set; } = new();
    public Dictionary<int, GuideInfo> Guides { get; private set; } = new();
    public Dictionary<int, string> Tourists { get; private set; } = new();

    public static async Task<MongoReferenceCache> LoadAsync(MongoContext ctx)
    {
        var all = FilterDefinition<NamedDocument>.Empty;
        return new MongoReferenceCache
        {
            Locations = (await ctx.Locations.Find(_ => true).ToListAsync()).ToDictionary(x => x.Id),
            Hotels = (await ctx.Hotels.Find(_ => true).ToListAsync()).ToDictionary(x => x.Id),
            Categories = (await ctx.Categories.Find(all).ToListAsync()).ToDictionary(x => x.Id, x => x.Name),
            Transports = (await ctx.TransportTypes.Find(all).ToListAsync()).ToDictionary(x => x.Id, x => x.Name),
            Tags = (await ctx.Tags.Find(all).ToListAsync()).ToDictionary(x => x.Id, x => x.Name),
            Guides = (await ctx.Guides.Find(_ => true).ToListAsync()).ToDictionary(x => x.Id),
            Tourists = (await ctx.Tourists.Find(_ => true).ToListAsync()).ToDictionary(x => x.Id, x => x.FullName)
        };
    }
}

public class MongoTourCatalogRepository : ITourCatalogRepository
{
    private readonly MongoContext _ctx;
    private readonly MongoReferenceCache _refs;
    private int _nextTourId;
    private int _nextReviewId;

    private MongoTourCatalogRepository(MongoContext ctx, MongoReferenceCache refs, int maxTourId, int maxReviewId)
    {
        _ctx = ctx;
        _refs = refs;
        _nextTourId = maxTourId;
        _nextReviewId = maxReviewId;
    }

    public static async Task<MongoTourCatalogRepository> CreateAsync(MongoContext ctx)
    {
        var refs = await MongoReferenceCache.LoadAsync(ctx);
        var maxTour = await ctx.Tours.Find(_ => true).SortByDescending(x => x.Id).Project(x => x.Id).FirstOrDefaultAsync();
        var maxReview = await ctx.Reviews.Find(_ => true).SortByDescending(x => x.Id).Project(x => x.Id).FirstOrDefaultAsync();
        return new MongoTourCatalogRepository(ctx, refs, maxTour, maxReview);
    }

    public string Name => "MongoDB";

    public async Task<TourDetails?> GetTourAsync(int id)
    {
        var doc = await _ctx.Tours.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        return doc is null ? null : ToDetails(doc);
    }

    public async Task<IReadOnlyList<TourSummary>> SearchAsync(SearchCriteria c)
    {
        var f = Builders<TourDocument>.Filter;
        var filter = f.Eq(x => x.IsDeleted, false)
                     & f.Eq("category._id", c.CategoryId)
                     & f.Eq("route.location.country", c.Country)
                     & f.Gte(x => x.Price, c.MinPrice)
                     & f.Lte(x => x.Price, c.MaxPrice);

        return await _ctx.Tours.Find(filter)
            .Sort(Builders<TourDocument>.Sort.Descending("rating.avg").Ascending("_id"))
            .Limit(c.Limit)
            .Project(x => new TourSummary
            {
                Id = x.Id,
                Title = x.Title,
                Price = x.Price,
                Category = x.Category.Name,
                AvgRating = x.Rating.Avg,
                ReviewsCount = x.Rating.Count
            })
            .ToListAsync();
    }

    public async Task<int> CreateTourAsync(NewTour t)
    {
        var id = Interlocked.Increment(ref _nextTourId);
        var attributes = new BsonDocument();
        foreach (var (k, v) in t.Attributes) attributes[k] = BsonValue.Create(v);

        var doc = new TourDocument
        {
            Id = id,
            Title = t.Title,
            Description = t.Description,
            Price = t.Price,
            DurationDays = t.DurationDays,
            Category = new CategoryInfo { Id = t.CategoryId, Name = _refs.Categories[t.CategoryId] },
            Guide = _refs.Guides[t.GuideId],
            Route = t.LocationIds.Select((loc, i) => new RouteStop { Order = i + 1, Location = _refs.Locations[loc] }).ToList(),
            Hotels = t.Hotels.Select(h =>
            {
                var hotel = _refs.Hotels[h.HotelId];
                return new HotelStay { Id = hotel.Id, Name = hotel.Name, Stars = hotel.Stars, Nights = h.Nights, Location = hotel.Location };
            }).ToList(),
            Transports = t.TransportIds.Select(x => new TransportInfo { Id = x, Name = _refs.Transports[x] }).ToList(),
            Attributes = attributes,
            Tags = t.TagIds.Select(x => _refs.Tags[x]).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = t.UpdatedBy
        };
        await _ctx.Tours.InsertOneAsync(doc);
        return id;
    }

    public async Task AddReviewAsync(NewReview r)
    {
        var review = new ReviewDocument
        {
            Id = Interlocked.Increment(ref _nextReviewId),
            TourId = r.TourId,
            TouristId = r.TouristId,
            Author = _refs.Tourists[r.TouristId],
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = MongoSeeder.Utc(r.CreatedAt)
        };
        await _ctx.Reviews.InsertOneAsync(review);

        var embedded = new BsonDocument
        {
            { "_id", review.Id }, { "author", review.Author }, { "rating", review.Rating },
            { "comment", (BsonValue?)review.Comment ?? BsonNull.Value }, { "createdAt", review.CreatedAt }
        };
        var pipeline = new EmptyPipelineDefinition<TourDocument>()
            .AppendStage<TourDocument, TourDocument, TourDocument>(new BsonDocument("$set", new BsonDocument
            {
                { "rating.count", new BsonDocument("$add", new BsonArray { "$rating.count", 1 }) },
                { "rating.sum", new BsonDocument("$add", new BsonArray { "$rating.sum", review.Rating }) },
                { "latestReviews", new BsonDocument("$slice", new BsonArray
                    {
                        new BsonDocument("$sortArray", new BsonDocument
                        {
                            { "input", new BsonDocument("$concatArrays", new BsonArray
                                {
                                    new BsonDocument("$literal", new BsonArray { embedded }), "$latestReviews"
                                }) },
                            { "sortBy", new BsonDocument { { "createdAt", -1 }, { "_id", -1 } } }
                        }),
                        5
                    })
                }
            }))
            .AppendStage<TourDocument, TourDocument, TourDocument>(new BsonDocument("$set", new BsonDocument
            {
                { "rating.avg", new BsonDocument("$divide", new BsonArray { "$rating.sum", "$rating.count" }) }
            }));

        await _ctx.Tours.UpdateOneAsync(x => x.Id == r.TourId, Builders<TourDocument>.Update.Pipeline(pipeline));
    }

    public async Task<IReadOnlyList<CountryStat>> CountryStatsAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("isDeleted", false)),
            new BsonDocument("$project", new BsonDocument
            {
                { "price", 1 },
                { "countries", new BsonDocument("$setUnion", new BsonArray { "$route.location.country" }) }
            }),
            new BsonDocument("$unwind", "$countries"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$countries" },
                { "toursCount", new BsonDocument("$sum", 1) },
                { "avgPrice", new BsonDocument("$avg", "$price") }
            }),
            new BsonDocument("$sort", new BsonDocument { { "toursCount", -1 }, { "_id", 1 } })
        };

        var rows = await _ctx.Tours.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return rows.Select(r => new CountryStat
        {
            Country = r["_id"].AsString,
            ToursCount = r["toursCount"].AsInt32,
            AvgPrice = Math.Round(r["avgPrice"].ToDecimal(), 2, MidpointRounding.AwayFromZero)
        }).ToList();
    }

    public async Task RenameHotelAsync(int hotelId, string newName)
    {
        await _ctx.Hotels.UpdateOneAsync(x => x.Id == hotelId, Builders<HotelDocument>.Update.Set(x => x.Name, newName));
        await _ctx.Tours.UpdateManyAsync(
            Builders<TourDocument>.Filter.Eq("hotels._id", hotelId),
            Builders<TourDocument>.Update.Set("hotels.$[h].name", newName),
            new UpdateOptions
            {
                ArrayFilters = new[] { new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("h._id", hotelId)) }
            });
        if (_refs.Hotels.TryGetValue(hotelId, out var cached)) cached.Name = newName;
    }

    private static TourDetails ToDetails(TourDocument d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Description = d.Description,
        Price = d.Price,
        DurationDays = d.DurationDays,
        Category = d.Category,
        Guide = d.Guide,
        Route = d.Route,
        Hotels = d.Hotels,
        Transports = d.Transports,
        Attributes = d.Attributes.ToDictionary(e => e.Name, e => BsonToText(e.Value)),
        Tags = d.Tags,
        AvgRating = d.Rating.Avg,
        ReviewsCount = d.Rating.Count,
        LatestReviews = d.LatestReviews
    };

    private static string BsonToText(BsonValue v) => v.BsonType switch
    {
        BsonType.Boolean => v.AsBoolean ? "true" : "false",
        BsonType.String => v.AsString,
        BsonType.Int32 => AttributeFormat.ToText(v.AsInt32),
        BsonType.Int64 => AttributeFormat.ToText(v.AsInt64),
        BsonType.Double => AttributeFormat.ToText(v.AsDouble),
        _ => v.ToString() ?? ""
    };
}

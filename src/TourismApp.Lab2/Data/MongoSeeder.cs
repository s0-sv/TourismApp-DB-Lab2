using MongoDB.Bson;
using MongoDB.Driver;
using TourismApp.Lab2.Domain;
using TourismApp.Lab2.Mongo;

namespace TourismApp.Lab2.Data;

public class MongoSeeder
{
    private readonly MongoContext _ctx;

    public MongoSeeder(MongoContext ctx) => _ctx = ctx;

    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task SeedAsync(Dataset d)
    {
        await _ctx.Database.Client.DropDatabaseAsync(_ctx.Database.DatabaseNamespace.DatabaseName);

        var locations = d.Locations.ToDictionary(l => l.Id, l => new LocationInfo { Id = l.Id, Name = l.Name, Country = l.Country, City = l.City });
        var hotels = d.Hotels.ToDictionary(h => h.Id);
        var categories = d.Categories.ToDictionary(c => c.Id, c => c.Name);
        var transports = d.TransportTypes.ToDictionary(c => c.Id, c => c.Name);
        var tags = d.Tags.ToDictionary(c => c.Id, c => c.Name);
        var usernames = d.Users.ToDictionary(u => u.Id, u => u.Username);
        var guides = d.Guides.ToDictionary(g => g.Id, g => new GuideInfo { Id = g.Id, Name = usernames[g.UserId], Bio = g.Bio, Rating = g.Rating });
        var tourists = d.Tourists.ToDictionary(t => t.Id, t => $"{t.FirstName} {t.LastName}");

        await _ctx.Locations.InsertManyAsync(locations.Values);
        await _ctx.Hotels.InsertManyAsync(d.Hotels.Select(h => new HotelDocument
        {
            Id = h.Id, Name = h.Name, Stars = h.Stars, Location = locations[h.LocationId]
        }));
        await _ctx.Categories.InsertManyAsync(categories.Select(c => new NamedDocument { Id = c.Key, Name = c.Value }));
        await _ctx.TransportTypes.InsertManyAsync(transports.Select(c => new NamedDocument { Id = c.Key, Name = c.Value }));
        await _ctx.Tags.InsertManyAsync(tags.Select(c => new NamedDocument { Id = c.Key, Name = c.Value }));
        await _ctx.Guides.InsertManyAsync(guides.Values);
        await _ctx.Tourists.InsertManyAsync(tourists.Select(t => new TouristDocument { Id = t.Key, FullName = t.Value }));

        var routeByTour = d.TourLocations.ToLookup(x => x.TourId);
        var hotelsByTour = d.TourHotels.ToLookup(x => x.TourId);
        var transportsByTour = d.TourTransports.ToLookup(x => x.TourId);
        var attrsByTour = d.TourAttributes.ToLookup(x => x.TourId);
        var tagsByTour = d.TourTags.ToLookup(x => x.TourId);
        var reviewsByTour = d.Reviews.ToLookup(x => x.TourId);

        var batch = new List<TourDocument>(2000);
        foreach (var t in d.Tours)
        {
            var reviews = reviewsByTour[t.Id].ToList();
            var sum = reviews.Sum(r => r.Rating);
            var attributes = new BsonDocument();
            foreach (var a in attrsByTour[t.Id]) attributes[a.Key] = BsonValue.Create(a.Value);

            batch.Add(new TourDocument
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Price = t.Price,
                DurationDays = t.DurationDays,
                IsDeleted = t.IsDeleted,
                Category = new CategoryInfo { Id = t.CategoryId, Name = categories[t.CategoryId] },
                Guide = guides[t.GuideId],
                Route = routeByTour[t.Id].OrderBy(r => r.VisitOrder)
                    .Select(r => new RouteStop { Order = r.VisitOrder, Location = locations[r.LocationId] }).ToList(),
                Hotels = hotelsByTour[t.Id].OrderBy(h => h.Id).Select(h => new HotelStay
                {
                    Id = h.HotelId, Name = hotels[h.HotelId].Name, Stars = hotels[h.HotelId].Stars, Nights = h.Nights,
                    Location = locations[hotels[h.HotelId].LocationId]
                }).ToList(),
                Transports = transportsByTour[t.Id].OrderBy(x => x.Id)
                    .Select(x => new TransportInfo { Id = x.TransportTypeId, Name = transports[x.TransportTypeId] }).ToList(),
                Attributes = attributes,
                Tags = tagsByTour[t.Id].Select(x => tags[x.TagId]).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                Rating = new RatingSummary { Count = reviews.Count, Sum = sum, Avg = reviews.Count == 0 ? 0 : (double)sum / reviews.Count },
                LatestReviews = reviews.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(5)
                    .Select(r => new ReviewInfo { Id = r.Id, Author = tourists[r.TouristId], Rating = r.Rating, Comment = r.Comment, CreatedAt = Utc(r.CreatedAt) })
                    .ToList(),
                UpdatedAt = Utc(t.UpdatedAt),
                UpdatedBy = t.UpdatedBy
            });
            if (batch.Count == batch.Capacity)
            {
                await _ctx.Tours.InsertManyAsync(batch);
                batch.Clear();
            }
        }
        if (batch.Count > 0) await _ctx.Tours.InsertManyAsync(batch);

        foreach (var chunk in d.Reviews.Chunk(10000))
        {
            await _ctx.Reviews.InsertManyAsync(chunk.Select(r => new ReviewDocument
            {
                Id = r.Id, TourId = r.TourId, TouristId = r.TouristId, Author = tourists[r.TouristId],
                Rating = r.Rating, Comment = r.Comment, CreatedAt = Utc(r.CreatedAt)
            }));
        }

        await _ctx.CreateIndexesAsync();
    }
}

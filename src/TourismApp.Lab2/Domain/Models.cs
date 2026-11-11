namespace TourismApp.Lab2.Domain;

public class LocationInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public string? City { get; set; }
}

public class RouteStop
{
    public int Order { get; set; }
    public LocationInfo Location { get; set; } = new();
}

public class HotelStay
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Stars { get; set; }
    public int Nights { get; set; }
    public LocationInfo Location { get; set; } = new();
}

public class TransportInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CategoryInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class GuideInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Bio { get; set; }
    public decimal Rating { get; set; }
}

public class ReviewInfo
{
    public int Id { get; set; }
    public string Author { get; set; } = "";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TourDetails
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public CategoryInfo Category { get; set; } = new();
    public GuideInfo Guide { get; set; } = new();
    public List<RouteStop> Route { get; set; } = new();
    public List<HotelStay> Hotels { get; set; } = new();
    public List<TransportInfo> Transports { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public double AvgRating { get; set; }
    public int ReviewsCount { get; set; }
    public List<ReviewInfo> LatestReviews { get; set; } = new();
}

public class TourSummary
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public string Category { get; set; } = "";
    public double AvgRating { get; set; }
    public int ReviewsCount { get; set; }
}

public class CountryStat
{
    public string Country { get; set; } = "";
    public int ToursCount { get; set; }
    public decimal AvgPrice { get; set; }
}

public record SearchCriteria(string Country, int CategoryId, decimal MinPrice, decimal MaxPrice, int Limit = 20);

public record HotelNights(int HotelId, int Nights);

public class NewTour
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int CategoryId { get; set; }
    public int GuideId { get; set; }
    public int UpdatedBy { get; set; }
    public List<int> LocationIds { get; set; } = new();
    public List<HotelNights> Hotels { get; set; } = new();
    public List<int> TransportIds { get; set; } = new();
    public Dictionary<string, object> Attributes { get; set; } = new();
    public List<int> TagIds { get; set; } = new();
}

public record NewReview(int TourId, int TouristId, int Rating, string? Comment, DateTime CreatedAt);

public static class AttributeFormat
{
    public static string ToText(object value) => value switch
    {
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}

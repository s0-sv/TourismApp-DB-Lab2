using TourismApp.Lab2.Domain;

namespace TourismApp.Lab2.Data;

public record UserRow(int Id, string Username, string Email, string PasswordHash, int RoleId, DateTime CreatedAt);
public record TouristRow(int Id, int UserId, string FirstName, string LastName, string Phone);
public record GuideRow(int Id, int UserId, string Bio, decimal Rating);
public record LocationRow(int Id, string Name, string Country, string City);
public record HotelRow(int Id, int LocationId, string Name, int Stars);
public record TourLocationRow(int Id, int TourId, int LocationId, int VisitOrder);
public record TourHotelRow(int Id, int TourId, int HotelId, int Nights);
public record TourTransportRow(int Id, int TourId, int TransportTypeId);
public record TourAttributeRow(int Id, int TourId, string Key, object Value);
public record TourTagRow(int TourId, int TagId);
public record ReviewRow(int Id, int TourId, int TouristId, int Rating, string Comment, DateTime CreatedAt);
public record BookingRow(int Id, int TourId, int TouristId, DateTime BookingDate, string Status);
public record PaymentRow(int Id, int BookingId, decimal Amount, DateTime PaymentDate, string Status);

public class TourRow
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Price { get; init; }
    public int DurationDays { get; init; }
    public int GuideId { get; init; }
    public int CategoryId { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int UpdatedBy { get; init; }
}

public class Dataset
{
    public List<(int Id, string Name)> Roles { get; } = new();
    public List<UserRow> Users { get; } = new();
    public List<TouristRow> Tourists { get; } = new();
    public List<GuideRow> Guides { get; } = new();
    public List<(int Id, string Name)> Categories { get; } = new();
    public List<LocationRow> Locations { get; } = new();
    public List<HotelRow> Hotels { get; } = new();
    public List<(int Id, string Name)> TransportTypes { get; } = new();
    public List<(int Id, string Name)> Tags { get; } = new();
    public List<TourRow> Tours { get; } = new();
    public List<TourLocationRow> TourLocations { get; } = new();
    public List<TourHotelRow> TourHotels { get; } = new();
    public List<TourTransportRow> TourTransports { get; } = new();
    public List<TourAttributeRow> TourAttributes { get; } = new();
    public List<TourTagRow> TourTags { get; } = new();
    public List<ReviewRow> Reviews { get; } = new();
    public List<BookingRow> Bookings { get; } = new();
    public List<PaymentRow> Payments { get; } = new();
}

public static class Catalog
{
    public static readonly (string Country, string[] Cities)[] Geography =
    {
        ("Україна", new[] { "Київ", "Львів", "Одеса", "Яремче", "Чернівці" }),
        ("Франція", new[] { "Париж", "Ніцца", "Ліон", "Марсель" }),
        ("Італія", new[] { "Рим", "Венеція", "Флоренція", "Мілан", "Неаполь" }),
        ("Іспанія", new[] { "Барселона", "Мадрид", "Севілья", "Валенсія" }),
        ("Португалія", new[] { "Лісабон", "Порту", "Фару" }),
        ("Німеччина", new[] { "Берлін", "Мюнхен", "Гамбург", "Кельн" }),
        ("Австрія", new[] { "Відень", "Зальцбург", "Інсбрук" }),
        ("Швейцарія", new[] { "Цюрих", "Женева", "Люцерн", "Церматт" }),
        ("Польща", new[] { "Краків", "Варшава", "Гданськ", "Закопане" }),
        ("Чехія", new[] { "Прага", "Брно", "Карлові Вари" }),
        ("Угорщина", new[] { "Будапешт", "Дебрецен", "Хевіз" }),
        ("Хорватія", new[] { "Дубровник", "Спліт", "Загреб" }),
        ("Греція", new[] { "Афіни", "Салоніки", "Іракліон", "Родос" }),
        ("Туреччина", new[] { "Стамбул", "Анталья", "Кападокія", "Бодрум" }),
        ("Єгипет", new[] { "Каїр", "Хургада", "Луксор" }),
        ("Нідерланди", new[] { "Амстердам", "Роттердам", "Утрехт" }),
        ("Бельгія", new[] { "Брюссель", "Брюгге", "Гент" }),
        ("Норвегія", new[] { "Осло", "Берген", "Тромсе" }),
        ("Ісландія", new[] { "Рейк'явік", "Акурейрі" }),
        ("Велика Британія", new[] { "Лондон", "Единбург", "Манчестер" }),
        ("Ірландія", new[] { "Дублін", "Голвей" }),
        ("Японія", new[] { "Токіо", "Кіото", "Осака" }),
        ("Таїланд", new[] { "Бангкок", "Пхукет", "Чіангмай" }),
        ("ОАЕ", new[] { "Дубай", "Абу-Дабі" }),
        ("Грузія", new[] { "Тбілісі", "Батумі", "Казбегі" }),
        ("Чорногорія", new[] { "Будва", "Котор" }),
        ("Мексика", new[] { "Канкун", "Мехіко" }),
        ("США", new[] { "Нью-Йорк", "Лос-Анджелес", "Маямі" })
    };

    public static readonly string[] Sights =
    {
        "Старе місто", "Центральна площа", "Національний музей", "Набережна", "Ботанічний сад",
        "Фортеця", "Кафедральний собор", "Оглядовий майданчик", "Національний парк", "Центральний ринок",
        "Палац", "Художня галерея"
    };

    public static readonly string[] Categories =
    {
        "Екскурсійний", "Екстрим", "Гірський туризм", "Пляжний відпочинок",
        "Гастрономічний", "Круїз", "Лижний", "Сімейний"
    };

    public static readonly string[] TransportTypes =
    {
        "Автобус", "Літак", "Потяг", "Пором", "Мікроавтобус", "Круїзний лайнер", "Велосипед", "Канатна дорога"
    };

    public static readonly string[] Tags =
    {
        "UNESCO", "для пар", "з дітьми", "бюджетний", "преміум", "фототур", "активний", "релакс",
        "історія", "архітектура", "природа", "море", "гори", "вино", "кухня", "шопінг", "фестиваль",
        "зима", "літо", "вихідні", "без візи", "гарячий тур", "ексклюзив", "індивідуальний", "груповий",
        "нічне життя", "SPA", "трекінг", "озера", "острови"
    };

    public static readonly string[] HotelWords =
    {
        "Гранд", "Палас", "Рів'єра", "Панорама", "Бутік", "Резорт", "Сіті", "Плаза", "Лагуна", "Альпіна"
    };

    public static readonly string[] FirstNames =
    {
        "Максим", "Ігор", "Олена", "Анна", "Софія", "Дмитро", "Андрій", "Марія", "Ірина", "Тарас",
        "Оксана", "Богдан", "Наталія", "Юлія", "Олег", "Катерина", "Василь", "Вікторія", "Роман", "Дарина"
    };

    public static readonly string[] LastNames =
    {
        "Коваленко", "Шевченко", "Бондаренко", "Ткаченко", "Кравченко", "Олійник", "Мельник", "Шевчук",
        "Поліщук", "Бойко", "Лисенко", "Руденко", "Савченко", "Петренко", "Марченко", "Гончаренко"
    };

    public static readonly string[] ReviewComments =
    {
        "Чудовий тур, все сподобалось!", "Гід дуже професійний.", "Забагато переїздів.",
        "Готель кращий, ніж очікували.", "Організація на висоті.", "Ціна відповідає якості.",
        "Хотілося б більше вільного часу.", "Рекомендую друзям!", "Погода підвела, але враження чудові.",
        "Трохи затягнуті екскурсії.", "Неймовірні краєвиди.", "Поїдемо ще раз наступного року."
    };
}

public class DatasetGenerator
{
    public const int TouristRoleId = 2;
    public const int GuideRoleId = 3;

    private readonly Random _rnd;
    private readonly int _tours;
    private readonly int _tourists;
    private readonly int _guides;
    private readonly int _bookings;
    private readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Unspecified);

    public DatasetGenerator(int tours = 20000, int tourists = 5000, int guides = 100, int bookings = 30000, int seed = 42)
    {
        _rnd = new Random(seed);
        _tours = tours;
        _tourists = tourists;
        _guides = guides;
        _bookings = bookings;
    }

    public Dataset Generate()
    {
        var d = new Dataset();
        d.Roles.AddRange(new[] { (1, "admin"), (2, "tourist"), (3, "guide") });

        d.Users.Add(new UserRow(1, "admin", "admin@tourism.test", "hash", 1, _now.AddYears(-3)));
        for (var i = 1; i <= _guides; i++)
        {
            var uid = d.Users.Count + 1;
            d.Users.Add(new UserRow(uid, $"guide{i}", $"guide{i}@tourism.test", "hash", GuideRoleId, RandomPast(1000)));
            d.Guides.Add(new GuideRow(i, uid, $"Гід з досвідом {_rnd.Next(1, 25)} років, мови: {Pick(new[] { "uk", "uk, en", "uk, en, de", "uk, pl", "uk, fr, it" })}.",
                Math.Round((decimal)(3.5 + _rnd.NextDouble() * 1.5), 2)));
        }
        for (var i = 1; i <= _tourists; i++)
        {
            var uid = d.Users.Count + 1;
            d.Users.Add(new UserRow(uid, $"tourist{i}", $"tourist{i}@tourism.test", "hash", TouristRoleId, RandomPast(1000)));
            d.Tourists.Add(new TouristRow(i, uid, Pick(Catalog.FirstNames), Pick(Catalog.LastNames), $"+380{_rnd.Next(500000000, 999999999)}"));
        }

        for (var i = 0; i < Catalog.Categories.Length; i++) d.Categories.Add((i + 1, Catalog.Categories[i]));
        for (var i = 0; i < Catalog.TransportTypes.Length; i++) d.TransportTypes.Add((i + 1, Catalog.TransportTypes[i]));
        for (var i = 0; i < Catalog.Tags.Length; i++) d.Tags.Add((i + 1, Catalog.Tags[i]));

        var locationsByCountry = new Dictionary<string, List<LocationRow>>();
        var hotelsByCity = new Dictionary<string, List<HotelRow>>();
        foreach (var (country, cities) in Catalog.Geography)
        {
            locationsByCountry[country] = new List<LocationRow>();
            foreach (var city in cities)
            {
                var cityLocations = new List<LocationRow>();
                foreach (var sight in Catalog.Sights)
                {
                    var loc = new LocationRow(d.Locations.Count + 1, $"{sight} ({city})", country, city);
                    d.Locations.Add(loc);
                    cityLocations.Add(loc);
                    locationsByCountry[country].Add(loc);
                }
                hotelsByCity[city] = new List<HotelRow>();
                for (var h = 0; h < 8; h++)
                {
                    var hotel = new HotelRow(d.Hotels.Count + 1, Pick(cityLocations).Id,
                        $"{Pick(Catalog.HotelWords)} {city} {h + 1}", _rnd.Next(2, 6));
                    d.Hotels.Add(hotel);
                    hotelsByCity[city].Add(hotel);
                }
            }
        }
        var countries = Catalog.Geography.Select(g => g.Country).ToArray();

        for (var t = 1; t <= _tours; t++)
        {
            var categoryId = _rnd.Next(1, d.Categories.Count + 1);
            var country = Pick(countries);
            var stops = _rnd.Next(2, 7);
            var route = new List<LocationRow>();
            while (route.Count < stops)
            {
                var pool = _rnd.NextDouble() < 0.85 ? locationsByCountry[country] : locationsByCountry[Pick(countries)];
                var candidate = Pick(pool);
                if (route.All(r => r.Id != candidate.Id)) route.Add(candidate);
            }
            var duration = _rnd.Next(1, 15);
            var mainCity = route[0].City;
            d.Tours.Add(new TourRow
            {
                Id = t,
                Title = $"{d.Categories[categoryId - 1].Name}: {mainCity} та околиці №{t}",
                Description = $"Тур на {duration} дн. Маршрут: {string.Join(" → ", route.Select(r => r.City).Distinct())}.",
                Price = Math.Round((decimal)(_rnd.Next(20, 1600) * 50), 2),
                DurationDays = duration,
                GuideId = _rnd.Next(1, _guides + 1),
                CategoryId = categoryId,
                IsDeleted = _rnd.NextDouble() < 0.03,
                UpdatedAt = RandomPast(400),
                UpdatedBy = 1
            });

            for (var i = 0; i < route.Count; i++)
                d.TourLocations.Add(new TourLocationRow(d.TourLocations.Count + 1, t, route[i].Id, i + 1));

            var hotelCount = duration == 1 ? 0 : _rnd.Next(1, 4);
            var usedHotels = new HashSet<int>();
            for (var i = 0; i < hotelCount; i++)
            {
                var hotel = Pick(hotelsByCity[Pick(route).City]);
                if (usedHotels.Add(hotel.Id))
                    d.TourHotels.Add(new TourHotelRow(d.TourHotels.Count + 1, t, hotel.Id, _rnd.Next(1, 6)));
            }

            foreach (var tr in Enumerable.Range(1, d.TransportTypes.Count).OrderBy(_ => _rnd.Next()).Take(_rnd.Next(1, 4)))
                d.TourTransports.Add(new TourTransportRow(d.TourTransports.Count + 1, t, tr));

            foreach (var (key, value) in GenerateAttributes(categoryId))
                d.TourAttributes.Add(new TourAttributeRow(d.TourAttributes.Count + 1, t, key, value));

            foreach (var tag in Enumerable.Range(1, d.Tags.Count).OrderBy(_ => _rnd.Next()).Take(_rnd.Next(2, 6)))
                d.TourTags.Add(new TourTagRow(t, tag));

            var reviewCount = _rnd.Next(0, 31);
            for (var i = 0; i < reviewCount; i++)
            {
                var rating = _rnd.NextDouble() switch { < 0.05 => 1, < 0.12 => 2, < 0.30 => 3, < 0.65 => 4, _ => 5 };
                d.Reviews.Add(new ReviewRow(d.Reviews.Count + 1, t, _rnd.Next(1, _tourists + 1), rating,
                    Pick(Catalog.ReviewComments), RandomPast(700)));
            }
        }

        string[] statuses = { "Pending", "Confirmed", "Confirmed", "Confirmed", "Cancelled" };
        for (var b = 1; b <= _bookings; b++)
        {
            var tour = d.Tours[_rnd.Next(d.Tours.Count)];
            var status = Pick(statuses);
            var date = RandomPast(365);
            d.Bookings.Add(new BookingRow(b, tour.Id, _rnd.Next(1, _tourists + 1), date, status));
            if (status == "Confirmed")
                d.Payments.Add(new PaymentRow(d.Payments.Count + 1, b, tour.Price, date.AddHours(_rnd.Next(1, 48)), "Completed"));
        }

        return d;
    }

    private IEnumerable<(string, object)> GenerateAttributes(int categoryId)
    {
        switch (categoryId)
        {
            case 1:
                yield return ("languages", Pick(new[] { "uk", "uk,en", "uk,en,de", "uk,pl" }));
                yield return ("audio_guide", _rnd.Next(2) == 1);
                yield return ("museum_tickets_included", _rnd.Next(2) == 1);
                yield return ("walking_km", _rnd.Next(3, 20));
                break;
            case 2:
                yield return ("difficulty", _rnd.Next(1, 6));
                yield return ("min_age", _rnd.Next(14, 21));
                yield return ("insurance_required", true);
                yield return ("equipment_provided", _rnd.Next(2) == 1);
                break;
            case 3:
                yield return ("max_altitude_m", _rnd.Next(800, 4800));
                yield return ("difficulty", _rnd.Next(1, 6));
                yield return ("tent_included", _rnd.Next(2) == 1);
                yield return ("distance_km", _rnd.Next(10, 120));
                break;
            case 4:
                yield return ("beach_type", Pick(new[] { "піщаний", "гальковий", "скелястий" }));
                yield return ("all_inclusive", _rnd.Next(2) == 1);
                yield return ("sea_view", _rnd.Next(2) == 1);
                yield return ("pool", _rnd.Next(2) == 1);
                break;
            case 5:
                yield return ("tastings_count", _rnd.Next(2, 12));
                yield return ("wine_included", _rnd.Next(2) == 1);
                yield return ("cuisine", Pick(new[] { "середземноморська", "українська", "азійська", "французька" }));
                yield return ("vegetarian_options", _rnd.Next(2) == 1);
                break;
            case 6:
                yield return ("ship_name", Pick(new[] { "Aurora", "Sea Breeze", "Nordic Star", "Odyssey" }));
                yield return ("cabin_class", Pick(new[] { "внутрішня", "з вікном", "з балконом", "люкс" }));
                yield return ("ports_count", _rnd.Next(2, 9));
                yield return ("deck_count", _rnd.Next(5, 16));
                break;
            case 7:
                yield return ("ski_pass_included", _rnd.Next(2) == 1);
                yield return ("slopes_km", _rnd.Next(20, 300));
                yield return ("equipment_rental", _rnd.Next(2) == 1);
                yield return ("level", Pick(new[] { "новачок", "середній", "профі" }));
                break;
            default:
                yield return ("min_child_age", _rnd.Next(0, 8));
                yield return ("kids_club", _rnd.Next(2) == 1);
                yield return ("animator", _rnd.Next(2) == 1);
                yield return ("family_rooms", _rnd.Next(2) == 1);
                break;
        }
        if (_rnd.NextDouble() < 0.4) yield return ("visa_required", _rnd.Next(2) == 1);
        if (_rnd.NextDouble() < 0.3) yield return ("meals", Pick(new[] { "сніданки", "напівпансіон", "повний пансіон" }));
    }

    private DateTime RandomPast(int maxDays) =>
        _now.AddSeconds(-_rnd.Next(60, maxDays * 86400));

    private T Pick<T>(IReadOnlyList<T> items) => items[_rnd.Next(items.Count)];
}

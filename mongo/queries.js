db = db.getSiblingDB("tourism");

print("--- findOne: документ туру з вкладеними даними");
printjson(db.tours.findOne({ _id: 15 }, { title: 1, price: 1, category: 1, "route.location.city": 1, hotels: 1, attributes: 1, rating: 1 }));

print("--- find: пошук за країною, категорією та ціною, сортування за рейтингом");
db.tours.find(
  { isDeleted: false, "category._id": 3, "route.location.country": "Італія", price: { $gte: NumberDecimal("10000"), $lte: NumberDecimal("40000") } },
  { title: 1, price: 1, "rating.avg": 1 }
).sort({ "rating.avg": -1, _id: 1 }).limit(5).forEach(printjson);

print("--- find: пошук за довільним атрибутом (гнучка схема)");
db.tours.find({ "attributes.all_inclusive": true, "attributes.sea_view": true }, { title: 1, attributes: 1 }).limit(3).forEach(printjson);
print("кількість турів з max_altitude_m > 4000:", db.tours.countDocuments({ "attributes.max_altitude_m": { $gt: 4000 } }));

print("--- find: тури з тегами UNESCO та історія");
print(db.tours.countDocuments({ tags: { $all: ["UNESCO", "історія"] } }));

print("--- insertOne: новий тур одним документом");
db.tours.deleteOne({ _id: 999999 });
db.tours.insertOne({
  _id: 999999,
  title: "Демо-тур: Карпати",
  description: "Створено з mongosh",
  price: NumberDecimal("9900"),
  durationDays: 3,
  isDeleted: false,
  category: { _id: 3, name: "Гірський туризм" },
  guide: { _id: 1, name: "guide1" },
  route: [{ order: 1, location: { _id: 1, name: "Старе місто (Київ)", country: "Україна", city: "Київ" } }],
  hotels: [],
  transports: [{ _id: 1, name: "Автобус" }],
  attributes: { difficulty: 3, max_altitude_m: 2061, tent_included: true },
  tags: ["гори", "активний"],
  rating: { count: 0, sum: 0, avg: 0 },
  latestReviews: []
});
printjson(db.tours.findOne({ _id: 999999 }, { title: 1, attributes: 1 }));

print("--- updateOne: зміна ціни та додавання атрибута");
db.tours.updateOne({ _id: 999999 }, { $set: { price: NumberDecimal("8900"), "attributes.guide_language": "uk" }, $push: { tags: "знижка" } });
printjson(db.tours.findOne({ _id: 999999 }, { price: 1, attributes: 1, tags: 1 }));

print("--- aggregate: середня ціна та кількість турів по категоріях");
db.tours.aggregate([
  { $match: { isDeleted: false } },
  { $group: { _id: "$category.name", tours: { $sum: 1 }, avgPrice: { $avg: "$price" }, avgRating: { $avg: "$rating.avg" } } },
  { $sort: { tours: -1 } }
]).forEach(printjson);

print("--- aggregate: $lookup відгуків для туру");
db.tours.aggregate([
  { $match: { _id: 15 } },
  { $lookup: { from: "reviews", localField: "_id", foreignField: "tourId", as: "allReviews" } },
  { $project: { title: 1, reviewsInCollection: { $size: "$allReviews" }, embeddedCount: "$rating.count" } }
]).forEach(printjson);

print("--- deleteOne");
printjson(db.tours.deleteOne({ _id: 999999 }));

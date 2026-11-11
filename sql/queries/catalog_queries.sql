SELECT t.id, t.title, t.description, t.price, t.duration_days,
       c.name AS category, u.username AS guide, g.rating AS guide_rating
FROM tours t
JOIN tour_categories c ON c.id = t.category_id
JOIN guides g ON g.id = t.guide_id
JOIN users u ON u.id = g.user_id
WHERE t.id = 15 AND t.is_deleted = FALSE;

SELECT tl.visit_order, l.name, l.city, l.country
FROM tour_locations tl
JOIN locations l ON l.id = tl.location_id
WHERE tl.tour_id = 15
ORDER BY tl.visit_order;

SELECT h.name, h.star_rating, th.nights_count, l.city
FROM tour_hotels th
JOIN hotels h ON h.id = th.hotel_id
JOIN locations l ON l.id = h.location_id
WHERE th.tour_id = 15;

SELECT attr_key, attr_value FROM tour_attributes WHERE tour_id = 15;

SELECT t.id, t.title, t.price, rs.avg_rating
FROM tours t
CROSS JOIN LATERAL (
    SELECT COALESCE(AVG(r.rating), 0)::float8 AS avg_rating FROM reviews r WHERE r.tour_id = t.id) rs
WHERE t.is_deleted = FALSE
  AND t.category_id = 3
  AND t.price BETWEEN 10000 AND 40000
  AND EXISTS (
      SELECT 1 FROM tour_locations tl JOIN locations l ON l.id = tl.location_id
      WHERE tl.tour_id = t.id AND l.country = 'Італія')
ORDER BY rs.avg_rating DESC, t.id
LIMIT 5;

SELECT t.id, t.title
FROM tours t
JOIN tour_attributes a1 ON a1.tour_id = t.id AND a1.attr_key = 'all_inclusive' AND a1.attr_value = 'true'
JOIN tour_attributes a2 ON a2.tour_id = t.id AND a2.attr_key = 'sea_view' AND a2.attr_value = 'true'
LIMIT 3;

SELECT COUNT(*) AS high_altitude_tours
FROM tour_attributes
WHERE attr_key = 'max_altitude_m' AND attr_value::int > 4000;

SELECT COUNT(*) AS unesco_history_tours
FROM (
    SELECT tt.tour_id
    FROM tour_tags tt
    JOIN tags tg ON tg.id = tt.tag_id
    WHERE tg.name IN ('UNESCO', 'історія')
    GROUP BY tt.tour_id
    HAVING COUNT(*) = 2
) x;

SELECT c.name, COUNT(*) AS tours, ROUND(AVG(t.price), 2) AS avg_price
FROM tours t
JOIN tour_categories c ON c.id = t.category_id
WHERE t.is_deleted = FALSE
GROUP BY c.name
ORDER BY tours DESC;

SELECT country, COUNT(*) AS tours, ROUND(AVG(price), 2) AS avg_price
FROM (
    SELECT DISTINCT l.country, t.id, t.price
    FROM tours t
    JOIN tour_locations tl ON tl.tour_id = t.id
    JOIN locations l ON l.id = tl.location_id
    WHERE t.is_deleted = FALSE
) x
GROUP BY country
ORDER BY tours DESC, country COLLATE "C";

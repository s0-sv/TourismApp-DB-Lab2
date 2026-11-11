CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role_id INT REFERENCES roles(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE tourists (
    id SERIAL PRIMARY KEY,
    user_id INT UNIQUE NOT NULL REFERENCES users(id),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(20)
);

CREATE TABLE guides (
    id SERIAL PRIMARY KEY,
    user_id INT UNIQUE NOT NULL REFERENCES users(id),
    bio TEXT,
    rating DECIMAL(3,2) DEFAULT 0
);

CREATE TABLE tour_categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE tours (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    price DECIMAL(10, 2) NOT NULL,
    duration_days INT NOT NULL DEFAULT 1,
    guide_id INT REFERENCES guides(id),
    category_id INT REFERENCES tour_categories(id),
    is_deleted BOOLEAN DEFAULT FALSE,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INT REFERENCES users(id)
);

CREATE TABLE locations (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    country VARCHAR(100) NOT NULL,
    city VARCHAR(100)
);

CREATE TABLE tour_locations (
    id SERIAL PRIMARY KEY,
    tour_id INT REFERENCES tours(id),
    location_id INT REFERENCES locations(id),
    visit_order INT NOT NULL
);

CREATE TABLE bookings (
    id SERIAL PRIMARY KEY,
    tour_id INT REFERENCES tours(id),
    tourist_id INT REFERENCES tourists(id),
    booking_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) DEFAULT 'Pending',
    is_deleted BOOLEAN DEFAULT FALSE,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by INT REFERENCES users(id)
);

CREATE TABLE payments (
    id SERIAL PRIMARY KEY,
    booking_id INT UNIQUE NOT NULL REFERENCES bookings(id),
    amount DECIMAL(10, 2) NOT NULL,
    payment_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) DEFAULT 'Completed'
);

CREATE TABLE reviews (
    id SERIAL PRIMARY KEY,
    tour_id INT REFERENCES tours(id),
    tourist_id INT REFERENCES tourists(id),
    rating INT CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE hotels (
    id SERIAL PRIMARY KEY,
    location_id INT REFERENCES locations(id),
    name VARCHAR(255) NOT NULL,
    star_rating INT CHECK (star_rating >= 1 AND star_rating <= 5)
);

CREATE TABLE tour_hotels (
    id SERIAL PRIMARY KEY,
    tour_id INT REFERENCES tours(id),
    hotel_id INT REFERENCES hotels(id),
    nights_count INT NOT NULL
);

CREATE TABLE transport_types (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE tour_transports (
    id SERIAL PRIMARY KEY,
    tour_id INT REFERENCES tours(id),
    transport_type_id INT REFERENCES transport_types(id)
);

CREATE TABLE tour_attributes (
    id SERIAL PRIMARY KEY,
    tour_id INT NOT NULL REFERENCES tours(id),
    attr_key VARCHAR(100) NOT NULL,
    attr_value VARCHAR(255) NOT NULL,
    UNIQUE (tour_id, attr_key)
);

CREATE TABLE tags (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE tour_tags (
    tour_id INT NOT NULL REFERENCES tours(id),
    tag_id INT NOT NULL REFERENCES tags(id),
    PRIMARY KEY (tour_id, tag_id)
);

CREATE INDEX idx_tours_category ON tours(category_id);
CREATE INDEX idx_reviews_tour_tourist ON reviews(tour_id, tourist_id);
CREATE INDEX idx_active_tours ON tours(id) WHERE is_deleted = FALSE;

CREATE INDEX idx_tours_category_price ON tours(category_id, price) WHERE is_deleted = FALSE;
CREATE INDEX idx_tour_locations_tour ON tour_locations(tour_id, visit_order);
CREATE INDEX idx_tour_locations_location ON tour_locations(location_id);
CREATE INDEX idx_locations_country ON locations(country);
CREATE INDEX idx_tour_hotels_tour ON tour_hotels(tour_id);
CREATE INDEX idx_tour_hotels_hotel ON tour_hotels(hotel_id);
CREATE INDEX idx_tour_transports_tour ON tour_transports(tour_id);
CREATE INDEX idx_reviews_tour_created ON reviews(tour_id, created_at DESC);
CREATE INDEX idx_tour_tags_tag ON tour_tags(tag_id);

CREATE OR REPLACE FUNCTION fn_update_timestamp() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_tours_update BEFORE UPDATE ON tours
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

CREATE TRIGGER trg_bookings_update BEFORE UPDATE ON bookings
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

CREATE OR REPLACE VIEW v_active_tours_details AS
SELECT t.id, t.title, t.price, c.name AS category, g.bio AS guide_info
FROM tours t
JOIN tour_categories c ON t.category_id = c.id
JOIN guides g ON t.guide_id = g.id
WHERE t.is_deleted = FALSE;

CREATE OR REPLACE VIEW v_active_bookings AS
SELECT b.id, t.title AS tour_title, tr.first_name || ' ' || tr.last_name AS tourist_name, b.status
FROM bookings b
JOIN tours t ON b.tour_id = t.id
JOIN tourists tr ON b.tourist_id = tr.id
WHERE b.is_deleted = FALSE;

CREATE OR REPLACE PROCEDURE sp_soft_delete_tour(p_tour_id INT, p_user_id INT)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE tours SET is_deleted = TRUE, updated_by = p_user_id WHERE id = p_tour_id;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_soft_delete_booking(p_booking_id INT, p_user_id INT)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE bookings SET is_deleted = TRUE, updated_by = p_user_id WHERE id = p_booking_id;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_create_tour(
    p_title VARCHAR, p_description TEXT, p_price DECIMAL, p_guide_id INT, p_category_id INT, p_user_id INT
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO tours (title, description, price, guide_id, category_id, updated_by)
    VALUES (p_title, p_description, p_price, p_guide_id, p_category_id, p_user_id);
END;
$$;

CREATE OR REPLACE FUNCTION fn_get_tour_avg_rating(p_tour_id INT) RETURNS DECIMAL AS $$
DECLARE avg_rating DECIMAL;
BEGIN
    SELECT COALESCE(AVG(rating), 0) INTO avg_rating FROM reviews WHERE tour_id = p_tour_id;
    RETURN avg_rating;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE sp_update_tour_price(p_tour_id INT, p_new_price DECIMAL, p_user_id INT)
LANGUAGE plpgsql AS $$
BEGIN
    UPDATE tours SET price = p_new_price, updated_by = p_user_id WHERE id = p_tour_id;
END;
$$;

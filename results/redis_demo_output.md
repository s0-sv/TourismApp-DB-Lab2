## 1. Рядки (STRING) + TTL: сесія користувача

> SET lab2:session:abc123 user:42 EX 1800
OK

> GET lab2:session:abc123
user:42

> TTL lab2:session:abc123
1799

> SET lab2:promo:SUMMER -10% NX
True

> SET lab2:promo:SUMMER -20% NX (повторно)
False

## 2. Лічильники: перегляди туру

> INCR lab2:tour:15:views (x5)
5

> INCRBY lab2:tour:15:views 100
105

## 3. Хеш (HASH): картка туру

> HSET lab2:tour:15 title ... price 25000 category ... free_seats 12
4

> HGET lab2:tour:15 price
25000

> HINCRBY lab2:tour:15 free_seats -2
10

> HGETALL lab2:tour:15
title=Романтичний Париж, price=25000, category=Екскурсійний, free_seats=10

> HEXISTS lab2:tour:15 discount
False

## 4. Список (LIST): нещодавно переглянуті тури

> LREM + LPUSH + LTRIM 0 4 (перегляди 3,15,8,42,15,7)
OK

> LRANGE lab2:user:42:recent 0 -1
7, 15, 42, 8, 3

> LLEN lab2:user:42:recent
5

## 5. Множина (SET): обрані тури

> SADD lab2:user:42:favorites 3 15 8 21
4

> SADD lab2:user:77:favorites 15 21 30
3

> SISMEMBER lab2:user:42:favorites 8
True

> SINTER lab2:user:42:favorites lab2:user:77:favorites
15, 21

> SUNION lab2:user:42:favorites lab2:user:77:favorites
3, 8, 15, 21, 30

> SREM lab2:user:42:favorites 3
True

> SCARD lab2:user:42:favorites
3

## 6. Впорядкована множина (ZSET): рейтинг популярності турів

> ZADD lab2:top:bookings 120 Париж 95 Рим 140 Говерла 80 Карпати
4

> ZINCRBY lab2:top:bookings 30 "Історичний Рим"
125

> ZREVRANGE lab2:top:bookings 0 2 WITHSCORES
Похід на Говерлу=140, Історичний Рим=125, Романтичний Париж=120

> ZREVRANK lab2:top:bookings "Лижний тур в Карпати"
3

> ZRANGEBYSCORE lab2:top:bookings 100 +inf
Романтичний Париж, Історичний Рим, Похід на Говерлу

## 7. Транзакція (MULTI/EXEC + WATCH): бронювання місця

> WATCH + MULTI; HINCRBY free_seats -1; RPUSH bookings user:42; EXEC
True

> HGET lab2:tour:15 free_seats
9

## 8. Pub/Sub: сповіщення про зміну ціни

> SUBSCRIBE lab2:tour-updates; PUBLISH lab2:tour-updates "tour:15 price=23000"
отримувачів: 1

> Повідомлення, отримане підписником
tour:15 price=23000

## 9. Кеш (cache-aside) для деталей туру з PostgreSQL

> GET (промах) -> SQL -> SET EX 300
268.735 мс

> GET (влучання)
0.163 мс, тур "Пляжний відпочинок: Брюссель та околиці №15"

## 10. Робота з ключами

> EXISTS lab2:tour:15
True

> TYPE lab2:top:bookings
SortedSet

> EXPIRE lab2:tour:15:views 60
True

> DEL lab2:promo:SUMMER
True

> SCAN 0 MATCH lab2:*
lab2:cache:tour:15, lab2:session:abc123, lab2:top:bookings, lab2:tour:15, lab2:tour:15:bookings, lab2:tour:15:views, lab2:user:42:favorites, lab2:user:42:recent, lab2:user:77:favorites

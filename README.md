# Hotel Reservation System VB.NET

A minimalist beige and brown hotel reservation prototype built for Microsoft Visual Studio using Visual Basic Windows Forms and a local SQLite database file.

## Features

- Room booking with availability checks
- Adult guest, child guest, and free child pax counts
- Children ages 1 to 3 are tracked as free pax
- Calendar-style room availability indicators using green/red status colors
- Room type, capacity, amenity, and availability status display
- Guest information capture
- Add-ons/reserved amenities
- Payment record creation
- Receipt generation and printable receipt page
- Reservation history
- Local email and guest alert notification queue
- Login without role selection; the system opens the correct UI from the saved account role
- Guest registration and seeded admin account
- Separate Admin dashboard and User booking interface
- Pending reservations that require admin confirmation
- Logout button for switching between Admin and User accounts
- SQLite database file created as `hotel_reservation.db` beside the executable

## Run In Visual Studio

1. Open `HotelReservation.sln` or `HotelReservation.vbproj` in Microsoft Visual Studio 2022.
2. Click **Build > Rebuild Solution**.
3. Press `F5` to run the VB.NET Windows Forms app.

The SQLite database is created automatically the first time the app runs. Required SQLite libraries are included in the `lib` folder.

## View the Database and Run Queries

The app stores data in `hotel_reservation.db` (created in `bin\Debug` when you run the project).

### Option 1: DB Browser for SQLite (recommended)

1. Download [DB Browser for SQLite](https://sqlitebrowser.org/).
2. Open `bin\Debug\hotel_reservation.db`.
3. Use the **Browse Data** tab to view tables.
4. Use the **Execute SQL** tab to run queries.

### Option 2: Use the included schema file

Open `database_schema.sql` for the table structure and example queries.

Example query:

```sql
SELECT r.ConfirmationCode, g.FullName, rm.RoomNumber, r.Status, p.Amount
FROM Reservations r
JOIN Guests g ON g.Id = r.GuestId
JOIN Rooms rm ON rm.Id = r.RoomId
LEFT JOIN Payments p ON p.ReservationId = r.Id
ORDER BY r.CreatedAt DESC;
```

## Default Admin Login

- Username: `admin`
- Password: `admin123`

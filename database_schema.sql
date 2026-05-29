-- Hotel Reservation SQLite schema
-- Open hotel_reservation.db in DB Browser for SQLite to browse data and run queries.

CREATE TABLE IF NOT EXISTS Accounts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName TEXT NOT NULL,
    Email TEXT NOT NULL,
    Phone TEXT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Rooms (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RoomNumber TEXT NOT NULL,
    Type TEXT NOT NULL,
    Capacity INTEGER NOT NULL,
    Rate REAL NOT NULL,
    Amenities TEXT,
    Status TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Guests (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName TEXT NOT NULL,
    Email TEXT NOT NULL,
    Phone TEXT NOT NULL,
    Address TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Reservations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfirmationCode TEXT NOT NULL UNIQUE,
    RoomId INTEGER NOT NULL,
    GuestId INTEGER NOT NULL,
    CheckIn TEXT NOT NULL,
    CheckOut TEXT NOT NULL,
    Guests INTEGER NOT NULL,
    AdultGuests INTEGER NOT NULL,
    ChildGuests INTEGER NOT NULL,
    FreeChildGuests INTEGER NOT NULL,
    Status TEXT NOT NULL,
    Notes TEXT,
    CreatedAt TEXT NOT NULL,
    AccountId INTEGER,
    FOREIGN KEY (RoomId) REFERENCES Rooms(Id),
    FOREIGN KEY (GuestId) REFERENCES Guests(Id)
);

CREATE TABLE IF NOT EXISTS AddOns (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    Price REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS ReservationAddOns (
    ReservationId INTEGER NOT NULL,
    AddOnId INTEGER NOT NULL,
    Quantity INTEGER NOT NULL,
    PRIMARY KEY (ReservationId, AddOnId),
    FOREIGN KEY (ReservationId) REFERENCES Reservations(Id),
    FOREIGN KEY (AddOnId) REFERENCES AddOns(Id)
);

CREATE TABLE IF NOT EXISTS Payments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReservationId INTEGER NOT NULL,
    Method TEXT NOT NULL,
    Amount REAL NOT NULL,
    Status TEXT NOT NULL,
    PaidAt TEXT NOT NULL,
    Reference TEXT,
    FOREIGN KEY (ReservationId) REFERENCES Reservations(Id)
);

CREATE TABLE IF NOT EXISTS Notifications (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReservationId INTEGER NOT NULL,
    Channel TEXT NOT NULL,
    Recipient TEXT NOT NULL,
    Subject TEXT NOT NULL,
    Message TEXT NOT NULL,
    Status TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (ReservationId) REFERENCES Reservations(Id)
);

-- Example queries:
-- SELECT * FROM Rooms;
-- SELECT * FROM Accounts;
-- SELECT r.ConfirmationCode, g.FullName, rm.RoomNumber, r.Status, p.Amount
-- FROM Reservations r
-- JOIN Guests g ON g.Id = r.GuestId
-- JOIN Rooms rm ON rm.Id = r.RoomId
-- LEFT JOIN Payments p ON p.ReservationId = r.Id
-- ORDER BY r.CreatedAt DESC;

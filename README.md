# Hotel Reservation System VB.NET

A minimalist beige and brown hotel reservation prototype built for Microsoft Visual Studio using Visual Basic Windows Forms and a local database file.

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
- Local database file created as `hotel_reservation_database.xml` beside the executable

## Run In Visual Studio

1. Open `HotelReservation.sln` or `HotelReservation.vbproj` in Microsoft Visual Studio.
2. Click **Build > Rebuild Solution**.
3. Press `F5` to run the VB.NET Windows Forms app.

There are no NuGet packages required. The local database is created automatically the first time the app runs.

## Default Admin Login

- Username: `admin`
- Password: `admin123`

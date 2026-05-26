Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

Namespace HotelReservation
    Public Class HotelRepository
        Private ReadOnly _databasePath As String
        Private ReadOnly _random As New Random()

        Public Sub New(databasePath As String)
            _databasePath = databasePath
        End Sub

        Public Sub Initialize()
            Dim database = LoadDatabase()
            If database.Tables("Rooms").Rows.Count = 0 Then
                SeedRooms(database)
            End If

            If database.Tables("AddOns").Rows.Count = 0 Then
                SeedAddOns(database)
            End If

            If database.Tables("Accounts").Rows.Count = 0 Then
                SeedAdminAccount(database)
            End If

            SaveDatabase(database)
        End Sub

        Public Function Authenticate(username As String, password As String) As AccountInfo
            Dim database = LoadDatabase()
            Dim account = FindAccountByUsername(database, username)
            If account Is Nothing Then
                Return Nothing
            End If

            If Not String.Equals(CStr(account("PasswordHash")), HashPassword(password), StringComparison.Ordinal) Then
                Return Nothing
            End If

            Return ToAccountInfo(account)
        End Function

        Public Function RegisterAccount(fullName As String, email As String, phone As String, username As String, password As String) As AccountInfo
            If String.IsNullOrWhiteSpace(fullName) OrElse
               String.IsNullOrWhiteSpace(email) OrElse
               String.IsNullOrWhiteSpace(username) OrElse
               String.IsNullOrWhiteSpace(password) Then
                Throw New ArgumentException("Full name, email, username, and password are required.")
            End If

            Dim database = LoadDatabase()
            If FindAccountByUsername(database, username) IsNot Nothing Then
                Throw New InvalidOperationException("That username is already registered.")
            End If

            Dim now = DateTime.UtcNow
            Dim accountId = NextId(database.Tables("Accounts"))
            database.Tables("Accounts").Rows.Add(
                accountId,
                fullName.Trim(),
                email.Trim(),
                If(String.IsNullOrWhiteSpace(phone), "", phone.Trim()),
                username.Trim(),
                HashPassword(password),
                "User",
                ToDateTime(now))

            SaveDatabase(database)
            Return ToAccountInfo(database.Tables("Accounts").Rows.Find(accountId))
        End Function

        Public Function GetAccounts() As List(Of AccountInfo)
            Dim database = LoadDatabase()
            Dim accounts As New List(Of AccountInfo)()

            For Each row As DataRow In database.Tables("Accounts").Rows
                accounts.Add(ToAccountInfo(row))
            Next

            Return accounts.OrderBy(Function(account) account.Role).ThenBy(Function(account) account.FullName).ToList()
        End Function

        Public Function GetRooms(Optional checkIn As Date? = Nothing, Optional checkOut As Date? = Nothing) As List(Of RoomInfo)
            Dim database = LoadDatabase()
            Dim rooms As New List(Of RoomInfo)()
            Dim hasDates = checkIn.HasValue AndAlso checkOut.HasValue AndAlso checkOut.Value.Date > checkIn.Value.Date

            For Each row As DataRow In database.Tables("Rooms").Rows
                Dim roomId = CInt(row("Id"))
                Dim baseStatus = CStr(row("Status"))
                Dim isAvailable = baseStatus = "Available"
                Dim status = baseStatus

                If hasDates AndAlso isAvailable AndAlso HasDateConflict(database, roomId, checkIn.Value, checkOut.Value) Then
                    isAvailable = False
                    status = "Not Available"
                End If

                rooms.Add(New RoomInfo With {
                    .Id = roomId,
                    .RoomNumber = CStr(row("RoomNumber")),
                    .RoomType = CStr(row("Type")),
                    .Capacity = CInt(row("Capacity")),
                    .Rate = CDec(row("Rate")),
                    .Amenities = CStr(row("Amenities")),
                    .Status = status,
                    .IsAvailable = isAvailable
                })
            Next

            Return rooms.OrderBy(Function(room) room.RoomType).ThenBy(Function(room) room.Rate).ToList()
        End Function

        Public Function GetRoomCalendar(roomId As Integer, startDate As Date, days As Integer) As List(Of AvailabilityDayInfo)
            Dim database = LoadDatabase()
            Dim calendar As New List(Of AvailabilityDayInfo)()

            For offset As Integer = 0 To days - 1
                Dim currentDate = startDate.Date.AddDays(offset)
                Dim isAvailable = Not HasDateConflict(database, roomId, currentDate, currentDate.AddDays(1))
                calendar.Add(New AvailabilityDayInfo With {
                    .Date = currentDate,
                    .IsAvailable = isAvailable,
                    .Status = If(isAvailable, "Available", "Not Available")
                })
            Next

            Return calendar
        End Function

        Public Function GetAddOns() As List(Of AddOnInfo)
            Dim database = LoadDatabase()
            Dim addOns As New List(Of AddOnInfo)()

            For Each row As DataRow In database.Tables("AddOns").Rows
                addOns.Add(New AddOnInfo With {
                    .Id = CInt(row("Id")),
                    .Name = CStr(row("Name")),
                    .Description = CStr(row("Description")),
                    .Price = CDec(row("Price"))
                })
            Next

            Return addOns.OrderBy(Function(addOn) addOn.Name).ToList()
        End Function

        Public Function CreateReservation(request As ReservationInput) As ReceiptInfo
            ValidateReservation(request)

            Dim database = LoadDatabase()
            Dim roomRow = database.Tables("Rooms").Rows.Find(request.RoomId)
            If roomRow Is Nothing Then
                Throw New ArgumentException("Please select a valid room.")
            End If

            If CInt(roomRow("Capacity")) < request.ChargeableGuests Then
                Throw New ArgumentException(String.Format("Room {0} can only fit {1} guest(s).", roomRow("RoomNumber"), roomRow("Capacity")))
            End If

            If HasDateConflict(database, request.RoomId, request.CheckIn, request.CheckOut) Then
                Throw New InvalidOperationException("This room is already reserved for the selected dates.")
            End If

            Dim nights = Math.Max(1, CInt((request.CheckOut.Date - request.CheckIn.Date).TotalDays))
            Dim roomSubtotal = nights * CDec(roomRow("Rate"))
            Dim selectedAddOns = BuildSelectedAddOns(database, request.AddOns)
            Dim addOnSubtotal = selectedAddOns.Sum(Function(addOn) addOn.Price * addOn.Quantity)
            Dim total = roomSubtotal + addOnSubtotal
            Dim now = DateTime.UtcNow
            Dim confirmationCode = GenerateConfirmationCode(database)

            Dim guestId = NextId(database.Tables("Guests"))
            database.Tables("Guests").Rows.Add(
                guestId,
                request.GuestName.Trim(),
                request.Email.Trim(),
                request.Phone.Trim(),
                If(String.IsNullOrWhiteSpace(request.Address), "", request.Address.Trim()),
                ToDateTime(now))

            Dim reservationId = NextId(database.Tables("Reservations"))
            Dim reservationRow = database.Tables("Reservations").NewRow()
            reservationRow("Id") = reservationId
            reservationRow("ConfirmationCode") = confirmationCode
            reservationRow("RoomId") = request.RoomId
            reservationRow("GuestId") = guestId
            reservationRow("CheckIn") = ToDate(request.CheckIn)
            reservationRow("CheckOut") = ToDate(request.CheckOut)
            reservationRow("Guests") = request.ChargeableGuests
            reservationRow("AdultGuests") = request.AdultGuests
            reservationRow("ChildGuests") = request.ChildGuests
            reservationRow("FreeChildGuests") = request.FreeChildGuests
            reservationRow("Status") = "Pending"
            reservationRow("Notes") = If(String.IsNullOrWhiteSpace(request.Notes), "", request.Notes.Trim())
            reservationRow("CreatedAt") = ToDateTime(now)
            database.Tables("Reservations").Rows.Add(reservationRow)

            For Each addOn In selectedAddOns
                database.Tables("ReservationAddOns").Rows.Add(reservationId, addOn.Id, addOn.Quantity)
            Next

            database.Tables("Payments").Rows.Add(
                NextId(database.Tables("Payments")),
                reservationId,
                request.PaymentMethod.Trim(),
                total,
                "Paid",
                ToDateTime(now),
                BuildPaymentReference(request.PaymentReference))

            QueueNotification(database, reservationId, "Email", request.Email.Trim(), "Reservation queued for admin verification",
                String.Format("Hello {0}, your reservation {1} for room {2} is queued and waiting for admin confirmation. Email delivery is simulated locally.",
                              request.GuestName.Trim(), confirmationCode, roomRow("RoomNumber")), now)

            QueueNotification(database, reservationId, "Alert", request.Phone.Trim(), "Arrival reminder",
                String.Format("Reminder: check-in starts on {0:MMM dd, yyyy}. Please bring a valid ID. Alert delivery is simulated locally.",
                              request.CheckIn), now)

            SaveDatabase(database)
            Return GetReceipt(confirmationCode)
        End Function

        Public Function GetReceipt(confirmationCode As String) As ReceiptInfo
            Dim database = LoadDatabase()
            Dim reservation = FindReservation(database, confirmationCode)
            If reservation Is Nothing Then
                Return Nothing
            End If

            Dim guest = database.Tables("Guests").Rows.Find(CInt(reservation("GuestId")))
            Dim room = database.Tables("Rooms").Rows.Find(CInt(reservation("RoomId")))
            Dim payment = FindPayment(database, CInt(reservation("Id")))
            Dim checkIn = ParseDate(CStr(reservation("CheckIn")))
            Dim checkOut = ParseDate(CStr(reservation("CheckOut")))
            Dim nights = Math.Max(1, CInt((checkOut.Date - checkIn.Date).TotalDays))
            Dim addOnLines = GetReceiptAddOns(database, CInt(reservation("Id")))
            Dim addOnSubtotal = addOnLines.Sum(Function(addOn) addOn.Total)

            Return New ReceiptInfo With {
                .ConfirmationCode = confirmationCode,
                .GuestName = CStr(guest("FullName")),
                .GuestEmail = CStr(guest("Email")),
                .GuestPhone = CStr(guest("Phone")),
                .RoomNumber = CStr(room("RoomNumber")),
                .RoomType = CStr(room("Type")),
                .Amenities = CStr(room("Amenities")),
                .CheckIn = checkIn,
                .CheckOut = checkOut,
                .Nights = nights,
                .Guests = CInt(reservation("Guests")),
                .AdultGuests = CInt(reservation("AdultGuests")),
                .ChildGuests = CInt(reservation("ChildGuests")),
                .FreeChildGuests = CInt(reservation("FreeChildGuests")),
                .ReservationStatus = CStr(reservation("Status")),
                .RoomSubtotal = nights * CDec(room("Rate")),
                .AddOns = addOnLines,
                .AddOnSubtotal = addOnSubtotal,
                .Total = CDec(payment("Amount")),
                .PaymentMethod = CStr(payment("Method")),
                .PaymentStatus = CStr(payment("Status")),
                .PaymentReference = CStr(payment("Reference")),
                .CreatedAt = ParseDateTime(CStr(reservation("CreatedAt")))
            }
        End Function

        Public Function GetReservationHistory() As List(Of ReservationHistoryInfo)
            Dim database = LoadDatabase()
            Dim history As New List(Of ReservationHistoryInfo)()

            For Each reservation As DataRow In database.Tables("Reservations").Rows
                Dim guest = database.Tables("Guests").Rows.Find(CInt(reservation("GuestId")))
                Dim room = database.Tables("Rooms").Rows.Find(CInt(reservation("RoomId")))
                Dim payment = FindPayment(database, CInt(reservation("Id")))

                history.Add(New ReservationHistoryInfo With {
                    .ConfirmationCode = CStr(reservation("ConfirmationCode")),
                    .GuestName = CStr(guest("FullName")),
                    .RoomNumber = CStr(room("RoomNumber")),
                    .RoomType = CStr(room("Type")),
                    .CheckIn = ParseDate(CStr(reservation("CheckIn"))),
                    .CheckOut = ParseDate(CStr(reservation("CheckOut"))),
                    .Guests = CInt(reservation("Guests")),
                    .AdultGuests = CInt(reservation("AdultGuests")),
                    .ChildGuests = CInt(reservation("ChildGuests")),
                    .FreeChildGuests = CInt(reservation("FreeChildGuests")),
                    .Status = CStr(reservation("Status")),
                    .Total = CDec(payment("Amount")),
                    .CreatedAt = ParseDateTime(CStr(reservation("CreatedAt")))
                })
            Next

            Return history.OrderByDescending(Function(item) item.CreatedAt).ToList()
        End Function

        Public Sub ConfirmReservation(confirmationCode As String)
            Dim database = LoadDatabase()
            Dim reservation = FindReservation(database, confirmationCode)
            If reservation Is Nothing Then
                Throw New ArgumentException("Please select a valid reservation to confirm.")
            End If

            If Not String.Equals(CStr(reservation("Status")), "Pending", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("Only pending reservations can be confirmed.")
            End If

            reservation("Status") = "Confirmed"
            Dim guest = database.Tables("Guests").Rows.Find(CInt(reservation("GuestId")))
            QueueNotification(database, CInt(reservation("Id")), "Email", CStr(guest("Email")), "Reservation confirmed by admin",
                String.Format("Your reservation {0} has been confirmed by the hotel admin.", confirmationCode), DateTime.UtcNow)
            SaveDatabase(database)
        End Sub

        Public Function GetNotifications() As List(Of NotificationInfo)
            Dim database = LoadDatabase()
            Dim notifications As New List(Of NotificationInfo)()

            For Each row As DataRow In database.Tables("Notifications").Rows
                Dim reservation = database.Tables("Reservations").Rows.Find(CInt(row("ReservationId")))
                notifications.Add(New NotificationInfo With {
                    .ConfirmationCode = CStr(reservation("ConfirmationCode")),
                    .Channel = CStr(row("Channel")),
                    .Recipient = CStr(row("Recipient")),
                    .Subject = CStr(row("Subject")),
                    .Message = CStr(row("Message")),
                    .Status = CStr(row("Status")),
                    .CreatedAt = ParseDateTime(CStr(row("CreatedAt")))
                })
            Next

            Return notifications.OrderByDescending(Function(item) item.CreatedAt).Take(30).ToList()
        End Function

        Private Function LoadDatabase() As DataSet
            Dim database = CreateSchema()
            If File.Exists(_databasePath) Then
                database.ReadXml(_databasePath, XmlReadMode.ReadSchema)
                EnsureAccountsTable(database)
                EnsureReservationGuestColumns(database)
                EnsurePrimaryKeys(database)
            End If

            Return database
        End Function

        Private Sub SaveDatabase(database As DataSet)
            Dim databaseDirectory = Path.GetDirectoryName(_databasePath)
            If Not String.IsNullOrEmpty(databaseDirectory) AndAlso Not IO.Directory.Exists(databaseDirectory) Then
                IO.Directory.CreateDirectory(databaseDirectory)
            End If

            database.WriteXml(_databasePath, XmlWriteMode.WriteSchema)
        End Sub

        Private Shared Function CreateSchema() As DataSet
            Dim database As New DataSet("HotelReservationDatabase")

            Dim rooms = CreateTable(database, "Rooms")
            rooms.Columns.Add("Id", GetType(Integer))
            rooms.Columns.Add("RoomNumber", GetType(String))
            rooms.Columns.Add("Type", GetType(String))
            rooms.Columns.Add("Capacity", GetType(Integer))
            rooms.Columns.Add("Rate", GetType(Decimal))
            rooms.Columns.Add("Amenities", GetType(String))
            rooms.Columns.Add("Status", GetType(String))

            Dim guests = CreateTable(database, "Guests")
            guests.Columns.Add("Id", GetType(Integer))
            guests.Columns.Add("FullName", GetType(String))
            guests.Columns.Add("Email", GetType(String))
            guests.Columns.Add("Phone", GetType(String))
            guests.Columns.Add("Address", GetType(String))
            guests.Columns.Add("CreatedAt", GetType(String))

            Dim reservations = CreateTable(database, "Reservations")
            reservations.Columns.Add("Id", GetType(Integer))
            reservations.Columns.Add("ConfirmationCode", GetType(String))
            reservations.Columns.Add("RoomId", GetType(Integer))
            reservations.Columns.Add("GuestId", GetType(Integer))
            reservations.Columns.Add("CheckIn", GetType(String))
            reservations.Columns.Add("CheckOut", GetType(String))
            reservations.Columns.Add("Guests", GetType(Integer))
            reservations.Columns.Add("AdultGuests", GetType(Integer))
            reservations.Columns.Add("ChildGuests", GetType(Integer))
            reservations.Columns.Add("FreeChildGuests", GetType(Integer))
            reservations.Columns.Add("Status", GetType(String))
            reservations.Columns.Add("Notes", GetType(String))
            reservations.Columns.Add("CreatedAt", GetType(String))

            Dim addOns = CreateTable(database, "AddOns")
            addOns.Columns.Add("Id", GetType(Integer))
            addOns.Columns.Add("Name", GetType(String))
            addOns.Columns.Add("Description", GetType(String))
            addOns.Columns.Add("Price", GetType(Decimal))

            Dim reservationAddOns = CreateTable(database, "ReservationAddOns")
            reservationAddOns.Columns.Add("ReservationId", GetType(Integer))
            reservationAddOns.Columns.Add("AddOnId", GetType(Integer))
            reservationAddOns.Columns.Add("Quantity", GetType(Integer))

            Dim payments = CreateTable(database, "Payments")
            payments.Columns.Add("Id", GetType(Integer))
            payments.Columns.Add("ReservationId", GetType(Integer))
            payments.Columns.Add("Method", GetType(String))
            payments.Columns.Add("Amount", GetType(Decimal))
            payments.Columns.Add("Status", GetType(String))
            payments.Columns.Add("PaidAt", GetType(String))
            payments.Columns.Add("Reference", GetType(String))

            Dim notifications = CreateTable(database, "Notifications")
            notifications.Columns.Add("Id", GetType(Integer))
            notifications.Columns.Add("ReservationId", GetType(Integer))
            notifications.Columns.Add("Channel", GetType(String))
            notifications.Columns.Add("Recipient", GetType(String))
            notifications.Columns.Add("Subject", GetType(String))
            notifications.Columns.Add("Message", GetType(String))
            notifications.Columns.Add("Status", GetType(String))
            notifications.Columns.Add("CreatedAt", GetType(String))

            Dim accounts = CreateTable(database, "Accounts")
            accounts.Columns.Add("Id", GetType(Integer))
            accounts.Columns.Add("FullName", GetType(String))
            accounts.Columns.Add("Email", GetType(String))
            accounts.Columns.Add("Phone", GetType(String))
            accounts.Columns.Add("Username", GetType(String))
            accounts.Columns.Add("PasswordHash", GetType(String))
            accounts.Columns.Add("Role", GetType(String))
            accounts.Columns.Add("CreatedAt", GetType(String))

            EnsurePrimaryKeys(database)
            Return database
        End Function

        Private Shared Function CreateTable(database As DataSet, tableName As String) As DataTable
            Dim table As New DataTable(tableName)
            database.Tables.Add(table)
            Return table
        End Function

        Private Shared Sub EnsurePrimaryKeys(database As DataSet)
            SetPrimaryKey(database.Tables("Rooms"), "Id")
            SetPrimaryKey(database.Tables("Guests"), "Id")
            SetPrimaryKey(database.Tables("Reservations"), "Id")
            SetPrimaryKey(database.Tables("AddOns"), "Id")
            SetPrimaryKey(database.Tables("Payments"), "Id")
            SetPrimaryKey(database.Tables("Notifications"), "Id")
            SetPrimaryKey(database.Tables("Accounts"), "Id")
        End Sub

        Private Shared Sub SetPrimaryKey(table As DataTable, columnName As String)
            If table.PrimaryKey Is Nothing OrElse table.PrimaryKey.Length = 0 Then
                table.PrimaryKey = New DataColumn() {table.Columns(columnName)}
            End If
        End Sub

        Private Shared Sub SeedRooms(database As DataSet)
            Dim rooms = database.Tables("Rooms")
            rooms.Rows.Add(1, "101", "Cozy Standard", 2, 3200D, "Queen bed, garden view, Wi-Fi, hot shower", "Available")
            rooms.Rows.Add(2, "102", "Garden Twin", 3, 3800D, "Twin beds, patio, Wi-Fi, coffee nook", "Available")
            rooms.Rows.Add(3, "201", "Deluxe Queen", 4, 5200D, "Queen bed, lounge chair, smart TV, minibar", "Available")
            rooms.Rows.Add(4, "202", "Sunset Suite", 4, 7200D, "King bed, balcony, bathtub, breakfast set", "Available")
            rooms.Rows.Add(5, "301", "Family Loft", 6, 9800D, "Two bedrooms, kitchenette, dining area, board games", "Available")
            rooms.Rows.Add(6, "302", "Premier Suite", 2, 11800D, "King bed, private balcony, bath ritual kit, late checkout", "Available")
        End Sub

        Private Shared Sub SeedAddOns(database As DataSet)
            Dim addOns = database.Tables("AddOns")
            addOns.Rows.Add(1, "Breakfast Tray", "Warm breakfast served to the room.", 650D)
            addOns.Rows.Add(2, "Airport Pickup", "Private car pickup for arriving guests.", 1800D)
            addOns.Rows.Add(3, "Spa Welcome Kit", "Bath salts, candle, and herbal tea.", 950D)
            addOns.Rows.Add(4, "Extra Bed", "Foldable bed with linen setup.", 1200D)
            addOns.Rows.Add(5, "Pet Care Pack", "Pet mat, bowl, and welcome treat.", 750D)
        End Sub

        Private Shared Sub SeedAdminAccount(database As DataSet)
            database.Tables("Accounts").Rows.Add(
                1,
                "System Administrator",
                "admin@casareserve.local",
                "0000000000",
                "admin",
                HashPassword("admin123"),
                "Admin",
                ToDateTime(DateTime.UtcNow))
        End Sub

        Private Shared Function HasDateConflict(database As DataSet, roomId As Integer, checkIn As Date, checkOut As Date) As Boolean
            For Each reservation As DataRow In database.Tables("Reservations").Rows
                Dim reservationStatus = CStr(reservation("Status"))
                If CInt(reservation("RoomId")) = roomId AndAlso
                   (reservationStatus = "Pending" OrElse reservationStatus = "Confirmed" OrElse reservationStatus = "Reserved" OrElse reservationStatus = "Checked In") Then
                    Dim existingCheckIn = ParseDate(CStr(reservation("CheckIn")))
                    Dim existingCheckOut = ParseDate(CStr(reservation("CheckOut")))
                    If existingCheckIn < checkOut.Date AndAlso existingCheckOut > checkIn.Date Then
                        Return True
                    End If
                End If
            Next

            Return False
        End Function

        Private Shared Function BuildSelectedAddOns(database As DataSet, selected As List(Of SelectedAddOn)) As List(Of SelectedAddOnDetail)
            Dim details As New List(Of SelectedAddOnDetail)()
            Dim grouped = selected.Where(Function(item) item.Quantity > 0).
                GroupBy(Function(item) item.AddOnId).
                Select(Function(group) New SelectedAddOn With {.AddOnId = group.Key, .Quantity = group.Sum(Function(item) item.Quantity)})

            For Each addOn In grouped
                Dim row = database.Tables("AddOns").Rows.Find(addOn.AddOnId)
                If row Is Nothing Then
                    Throw New ArgumentException("One selected add-on is no longer available.")
                End If

                details.Add(New SelectedAddOnDetail With {
                    .Id = CInt(row("Id")),
                    .Quantity = addOn.Quantity,
                    .Price = CDec(row("Price"))
                })
            Next

            Return details
        End Function

        Private Shared Function GetReceiptAddOns(database As DataSet, reservationId As Integer) As List(Of AddOnLine)
            Dim lines As New List(Of AddOnLine)()
            For Each row As DataRow In database.Tables("ReservationAddOns").Rows
                If CInt(row("ReservationId")) = reservationId Then
                    Dim addOn = database.Tables("AddOns").Rows.Find(CInt(row("AddOnId")))
                    Dim quantity = CInt(row("Quantity"))
                    Dim unitPrice = CDec(addOn("Price"))
                    lines.Add(New AddOnLine With {
                        .Name = CStr(addOn("Name")),
                        .Quantity = quantity,
                        .UnitPrice = unitPrice,
                        .Total = unitPrice * quantity
                    })
                End If
            Next

            Return lines.OrderBy(Function(line) line.Name).ToList()
        End Function

        Private Shared Function FindReservation(database As DataSet, confirmationCode As String) As DataRow
            For Each row As DataRow In database.Tables("Reservations").Rows
                If String.Equals(CStr(row("ConfirmationCode")), confirmationCode, StringComparison.OrdinalIgnoreCase) Then
                    Return row
                End If
            Next

            Return Nothing
        End Function

        Private Shared Function FindPayment(database As DataSet, reservationId As Integer) As DataRow
            For Each row As DataRow In database.Tables("Payments").Rows
                If CInt(row("ReservationId")) = reservationId Then
                    Return row
                End If
            Next

            Return Nothing
        End Function

        Private Shared Function FindAccountByUsername(database As DataSet, username As String) As DataRow
            For Each row As DataRow In database.Tables("Accounts").Rows
                If String.Equals(CStr(row("Username")), username.Trim(), StringComparison.OrdinalIgnoreCase) Then
                    Return row
                End If
            Next

            Return Nothing
        End Function

        Private Shared Function ToAccountInfo(row As DataRow) As AccountInfo
            Return New AccountInfo With {
                .Id = CInt(row("Id")),
                .FullName = CStr(row("FullName")),
                .Email = CStr(row("Email")),
                .Phone = CStr(row("Phone")),
                .Username = CStr(row("Username")),
                .Role = CStr(row("Role")),
                .CreatedAt = ParseDateTime(CStr(row("CreatedAt")))
            }
        End Function

        Private Shared Sub QueueNotification(database As DataSet, reservationId As Integer, channel As String, recipient As String, subject As String, message As String, createdAt As Date)
            database.Tables("Notifications").Rows.Add(
                NextId(database.Tables("Notifications")),
                reservationId,
                channel,
                recipient,
                subject,
                message,
                "Queued locally",
                ToDateTime(createdAt))
        End Sub

        Private Function GenerateConfirmationCode(database As DataSet) As String
            For attempt As Integer = 1 To 20
                Dim code = String.Format("HR-{0:yyMMdd}-{1}", DateTime.UtcNow, _random.Next(1000, 9999))
                If FindReservation(database, code) Is Nothing Then
                    Return code
                End If
            Next

            Throw New InvalidOperationException("Could not generate a reservation code. Please try again.")
        End Function

        Private Shared Function NextId(table As DataTable) As Integer
            If table.Rows.Count = 0 Then
                Return 1
            End If

            Return table.AsEnumerable().Max(Function(row) CInt(row("Id"))) + 1
        End Function

        Private Shared Sub ValidateReservation(request As ReservationInput)
            If request.CheckOut.Date <= request.CheckIn.Date Then
                Throw New ArgumentException("Check-out must be after check-in.")
            End If

            If request.AdultGuests <= 0 Then
                Throw New ArgumentException("Please enter at least one adult guest.")
            End If

            If request.ChildGuests < 0 OrElse request.FreeChildGuests < 0 Then
                Throw New ArgumentException("Children guest counts cannot be negative.")
            End If

            If String.IsNullOrWhiteSpace(request.GuestName) OrElse
               String.IsNullOrWhiteSpace(request.Email) OrElse
               String.IsNullOrWhiteSpace(request.Phone) Then
                Throw New ArgumentException("Guest name, email, and phone are required.")
            End If

            If String.IsNullOrWhiteSpace(request.PaymentMethod) Then
                Throw New ArgumentException("Please choose a payment method.")
            End If
        End Sub

        Private Shared Function BuildPaymentReference(reference As String) As String
            If Not String.IsNullOrWhiteSpace(reference) Then
                Return reference.Trim()
            End If

            Return String.Format("LOCAL-{0:yyyyMMddHHmmss}", DateTime.UtcNow)
        End Function

        Private Shared Function NormalizeRole(role As String) As String
            If String.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) Then
                Return "Admin"
            End If

            Return "User"
        End Function

        Private Shared Function HashPassword(password As String) As String
            Using sha = SHA256.Create()
                Dim bytes = Encoding.UTF8.GetBytes(password)
                Dim hash = sha.ComputeHash(bytes)
                Return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()
            End Using
        End Function

        Private Shared Sub EnsureAccountsTable(database As DataSet)
            If database.Tables.Contains("Accounts") Then
                Return
            End If

            Dim accounts = CreateTable(database, "Accounts")
            accounts.Columns.Add("Id", GetType(Integer))
            accounts.Columns.Add("FullName", GetType(String))
            accounts.Columns.Add("Email", GetType(String))
            accounts.Columns.Add("Phone", GetType(String))
            accounts.Columns.Add("Username", GetType(String))
            accounts.Columns.Add("PasswordHash", GetType(String))
            accounts.Columns.Add("Role", GetType(String))
            accounts.Columns.Add("CreatedAt", GetType(String))
        End Sub

        Private Shared Sub EnsureReservationGuestColumns(database As DataSet)
            Dim reservations = database.Tables("Reservations")
            If Not reservations.Columns.Contains("AdultGuests") Then
                reservations.Columns.Add("AdultGuests", GetType(Integer))
            End If
            If Not reservations.Columns.Contains("ChildGuests") Then
                reservations.Columns.Add("ChildGuests", GetType(Integer))
            End If
            If Not reservations.Columns.Contains("FreeChildGuests") Then
                reservations.Columns.Add("FreeChildGuests", GetType(Integer))
            End If

            For Each row As DataRow In reservations.Rows
                If row.IsNull("AdultGuests") Then
                    row("AdultGuests") = CInt(row("Guests"))
                End If
                If row.IsNull("ChildGuests") Then
                    row("ChildGuests") = 0
                End If
                If row.IsNull("FreeChildGuests") Then
                    row("FreeChildGuests") = 0
                End If
            Next
        End Sub

        Private Shared Function ToDate(value As Date) As String
            Return value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function ToDateTime(value As Date) As String
            Return value.ToString("O", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function ParseDate(value As String) As Date
            Return DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function ParseDateTime(value As String) As Date
            Return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        End Function

        Private Class SelectedAddOnDetail
            Public Property Id As Integer
            Public Property Quantity As Integer
            Public Property Price As Decimal
        End Class
    End Class
End Namespace

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.Data.Sqlite

Namespace HotelReservation
    Public Class HotelRepository
        Private ReadOnly _connectionString As String
        Private ReadOnly _random As New Random()

        Public Sub New(connectionString As String)
            _connectionString = connectionString
        End Sub

        Public Sub Initialize()
            EnsureDatabaseDirectory()
            Using connection = OpenConnection()
                CreateSchema(connection)
                EnsureSchemaUpgrades(connection)
                If GetRowCount(connection, "Rooms") = 0 Then
                    SeedRooms(connection)
                End If

                If GetRowCount(connection, "AddOns") = 0 Then
                    SeedAddOns(connection)
                End If

                If GetRowCount(connection, "Accounts") = 0 Then
                    SeedAdminAccount(connection)
                End If
            End Using
        End Sub

        Public Function Authenticate(username As String, password As String) As AccountInfo
            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT Id, FullName, Email, Phone, Username, Role, CreatedAt, PasswordHash
                        FROM Accounts
                        WHERE Username = @Username COLLATE NOCASE"
                    cmd.Parameters.AddWithValue("@Username", username.Trim())

                    Using reader = cmd.ExecuteReader()
                        If Not reader.Read() Then
                            Return Nothing
                        End If

                        If Not String.Equals(reader.GetString(7), HashPassword(password), StringComparison.Ordinal) Then
                            Return Nothing
                        End If

                        Return ReadAccountInfo(reader)
                    End Using
                End Using
            End Using
        End Function

        Public Function RegisterAccount(fullName As String, email As String, phone As String, username As String, password As String) As AccountInfo
            If String.IsNullOrWhiteSpace(fullName) OrElse
               String.IsNullOrWhiteSpace(email) OrElse
               String.IsNullOrWhiteSpace(username) OrElse
               String.IsNullOrWhiteSpace(password) Then
                Throw New ArgumentException("Full name, email, username, and password are required.")
            End If

            Using connection = OpenConnection()
                connection.Open()
                If AccountExists(connection, username) Then
                    Throw New InvalidOperationException("That username is already registered.")
                End If

                Dim now = ToDateTime(DateTime.UtcNow)
                Dim accountId As Long
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        INSERT INTO Accounts (FullName, Email, Phone, Username, PasswordHash, Role, CreatedAt)
                        VALUES (@FullName, @Email, @Phone, @Username, @PasswordHash, @Role, @CreatedAt);
                        SELECT last_insert_rowid();"
                    cmd.Parameters.AddWithValue("@FullName", fullName.Trim())
                    cmd.Parameters.AddWithValue("@Email", email.Trim())
                    cmd.Parameters.AddWithValue("@Phone", If(String.IsNullOrWhiteSpace(phone), "", phone.Trim()))
                    cmd.Parameters.AddWithValue("@Username", username.Trim())
                    cmd.Parameters.AddWithValue("@PasswordHash", HashPassword(password))
                    cmd.Parameters.AddWithValue("@Role", "User")
                    cmd.Parameters.AddWithValue("@CreatedAt", now)
                    accountId = CLng(cmd.ExecuteScalar())
                End Using

                Return GetAccountById(connection, CInt(accountId))
            End Using
        End Function

        Public Function GetAccounts() As List(Of AccountInfo)
            Dim accounts As New List(Of AccountInfo)()
            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT Id, FullName, Email, Phone, Username, Role, CreatedAt
                        FROM Accounts
                        ORDER BY Role, FullName"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            accounts.Add(ReadAccountInfo(reader))
                        End While
                    End Using
                End Using
            End Using

            Return accounts
        End Function

        Public Function GetRooms(Optional checkIn As Date? = Nothing, Optional checkOut As Date? = Nothing, Optional excludeReservationId As Integer? = Nothing) As List(Of RoomInfo)
            Dim rooms As New List(Of RoomInfo)()
            Dim hasDates = checkIn.HasValue AndAlso checkOut.HasValue AndAlso checkOut.Value.Date > checkIn.Value.Date

            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT Id, RoomNumber, Type, Capacity, Rate, Amenities, Status
                        FROM Rooms"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim roomId = reader.GetInt32(0)
                            Dim baseStatus = reader.GetString(6)
                            Dim isAvailable = baseStatus = "Available"
                            Dim status = baseStatus

                            If hasDates AndAlso isAvailable AndAlso HasDateConflict(connection, roomId, checkIn.Value, checkOut.Value, Nothing, excludeReservationId) Then
                                isAvailable = False
                                status = "Not Available"
                            End If

                            rooms.Add(New RoomInfo With {
                                .Id = roomId,
                                .RoomNumber = reader.GetString(1),
                                .RoomType = reader.GetString(2),
                                .Capacity = reader.GetInt32(3),
                                .Rate = reader.GetDecimal(4),
                                .Amenities = GetStringOrEmpty(reader, 5),
                                .Status = status,
                                .IsAvailable = isAvailable
                            })
                        End While
                    End Using
                End Using
            End Using

            Return rooms.OrderBy(Function(room) room.RoomType).ThenBy(Function(room) room.Rate).ToList()
        End Function

        Public Function GetRoomCalendar(roomId As Integer, startDate As Date, days As Integer) As List(Of AvailabilityDayInfo)
            Dim calendar As New List(Of AvailabilityDayInfo)()

            Using connection = OpenConnection()
                connection.Open()
                For offset As Integer = 0 To days - 1
                    Dim currentDate = startDate.Date.AddDays(offset)
                    Dim isAvailable = Not HasDateConflict(connection, roomId, currentDate, currentDate.AddDays(1))
                    calendar.Add(New AvailabilityDayInfo With {
                        .Date = currentDate,
                        .IsAvailable = isAvailable,
                        .Status = If(isAvailable, "Available", "Not Available")
                    })
                Next
            End Using

            Return calendar
        End Function

        Public Function GetAddOns() As List(Of AddOnInfo)
            Dim addOns As New List(Of AddOnInfo)()

            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "SELECT Id, Name, Description, Price FROM AddOns ORDER BY Name"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            addOns.Add(New AddOnInfo With {
                                .Id = reader.GetInt32(0),
                                .Name = reader.GetString(1),
                                .Description = GetStringOrEmpty(reader, 2),
                                .Price = reader.GetDecimal(3)
                            })
                        End While
                    End Using
                End Using
            End Using

            Return addOns
        End Function

        Public Function CreateReservation(request As ReservationInput) As ReceiptInfo
            ValidateReservation(request)

            Using connection = OpenConnection()
                connection.Open()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim room = GetRoomRow(connection, transaction, request.RoomId)
                        If room Is Nothing Then
                            Throw New ArgumentException("Please select a valid room.")
                        End If

                        If room.Capacity < request.ChargeableGuests Then
                            Throw New ArgumentException(String.Format("Room {0} can only fit {1} guest(s).", room.RoomNumber, room.Capacity))
                        End If

                        If HasDateConflict(connection, request.RoomId, request.CheckIn, request.CheckOut, transaction) Then
                            Throw New InvalidOperationException("This room is already reserved for the selected dates.")
                        End If

                        Dim nights = Math.Max(1, CInt((request.CheckOut.Date - request.CheckIn.Date).TotalDays))
                        Dim roomSubtotal = nights * room.Rate
                        Dim selectedAddOns = BuildSelectedAddOns(connection, transaction, request.AddOns)
                        Dim addOnSubtotal = selectedAddOns.Sum(Function(addOn) addOn.Price * addOn.Quantity)
                        Dim total = roomSubtotal + addOnSubtotal
                        Dim now = DateTime.UtcNow
                        Dim confirmationCode = GenerateConfirmationCode(connection, transaction)

                        Dim guestId = InsertGuest(connection, transaction, request, now)
                        Dim reservationId = InsertReservation(connection, transaction, request, confirmationCode, guestId, now)

                        For Each addOn In selectedAddOns
                            InsertReservationAddOn(connection, transaction, reservationId, addOn.Id, addOn.Quantity)
                        Next

                        InsertPayment(connection, transaction, reservationId, request, total, now)
                        QueueNotification(connection, transaction, reservationId, "Email", request.Email.Trim(), "Reservation queued for admin verification",
                            String.Format("Hello {0}, your reservation {1} for room {2} is queued and waiting for admin confirmation. Email delivery is simulated locally.",
                                          request.GuestName.Trim(), confirmationCode, room.RoomNumber), now)
                        QueueNotification(connection, transaction, reservationId, "Alert", request.Phone.Trim(), "Arrival reminder",
                            String.Format("Reminder: check-in starts on {0:MMM dd, yyyy}. Please bring a valid ID. Alert delivery is simulated locally.",
                                          request.CheckIn), now)
                        NotifyAdmins(connection, transaction, reservationId, "New reservation pending confirmation",
                            String.Format("Reservation {0} from {1} is queued and waiting for admin confirmation.", confirmationCode, request.GuestName.Trim()), now)

                        transaction.Commit()
                        Return GetReceipt(confirmationCode)
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Function GetReservationId(confirmationCode As String, accountId As Integer, guestEmail As String) As Integer?
            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT r.Id
                        FROM Reservations r
                        INNER JOIN Guests g ON g.Id = r.GuestId
                        WHERE r.ConfirmationCode = @ConfirmationCode COLLATE NOCASE
                          AND " & ReservationOwnedByUserClause()
                    AddAccountFilterParameters(cmd, accountId, guestEmail)
                    cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)
                    Dim result = cmd.ExecuteScalar()
                    If result Is Nothing OrElse result Is DBNull.Value Then
                        Return Nothing
                    End If
                    Return CInt(result)
                End Using
            End Using
        End Function

        Public Function GetReceiptForAccount(confirmationCode As String, accountId As Integer, guestEmail As String) As ReceiptInfo
            Using connection = OpenConnection()
                connection.Open()
                If Not ReservationOwnedByUser(connection, confirmationCode, accountId, guestEmail) Then
                    Return Nothing
                End If
            End Using

            Return GetReceipt(confirmationCode)
        End Function

        Public Function GetReceipt(confirmationCode As String) As ReceiptInfo
            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT
                            r.ConfirmationCode,
                            g.FullName,
                            g.Email,
                            g.Phone,
                            rm.RoomNumber,
                            rm.Type,
                            rm.Amenities,
                            rm.Rate,
                            r.CheckIn,
                            r.CheckOut,
                            r.Guests,
                            r.AdultGuests,
                            r.ChildGuests,
                            r.FreeChildGuests,
                            r.Status,
                            r.CreatedAt,
                            p.Amount,
                            p.Method,
                            p.Status,
                            p.Reference,
                            r.Id
                        FROM Reservations r
                        INNER JOIN Guests g ON g.Id = r.GuestId
                        INNER JOIN Rooms rm ON rm.Id = r.RoomId
                        INNER JOIN Payments p ON p.ReservationId = r.Id
                        WHERE r.ConfirmationCode = @ConfirmationCode COLLATE NOCASE"
                    cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)

                    Using reader = cmd.ExecuteReader()
                        If Not reader.Read() Then
                            Return Nothing
                        End If

                        Dim checkIn = ParseDate(reader.GetString(8))
                        Dim checkOut = ParseDate(reader.GetString(9))
                        Dim nights = Math.Max(1, CInt((checkOut.Date - checkIn.Date).TotalDays))
                        Dim reservationId = reader.GetInt32(20)
                        Dim addOnLines = GetReceiptAddOns(connection, reservationId)
                        Dim addOnSubtotal = addOnLines.Sum(Function(addOn) addOn.Total)

                        Return New ReceiptInfo With {
                            .ConfirmationCode = reader.GetString(0),
                            .GuestName = reader.GetString(1),
                            .GuestEmail = reader.GetString(2),
                            .GuestPhone = reader.GetString(3),
                            .RoomNumber = reader.GetString(4),
                            .RoomType = reader.GetString(5),
                            .Amenities = GetStringOrEmpty(reader, 6),
                            .CheckIn = checkIn,
                            .CheckOut = checkOut,
                            .Nights = nights,
                            .Guests = reader.GetInt32(10),
                            .AdultGuests = reader.GetInt32(11),
                            .ChildGuests = reader.GetInt32(12),
                            .FreeChildGuests = reader.GetInt32(13),
                            .ReservationStatus = reader.GetString(14),
                            .RoomSubtotal = nights * reader.GetDecimal(7),
                            .AddOns = addOnLines,
                            .AddOnSubtotal = addOnSubtotal,
                            .Total = reader.GetDecimal(16),
                            .PaymentMethod = reader.GetString(17),
                            .PaymentStatus = reader.GetString(18),
                            .PaymentReference = GetStringOrEmpty(reader, 19),
                            .CreatedAt = ParseDateTime(reader.GetString(15))
                        }
                    End Using
                End Using
            End Using
        End Function

        Public Function GetReservationHistory(Optional accountId As Integer? = Nothing, Optional guestEmail As String = Nothing) As List(Of ReservationHistoryInfo)
            Dim history As New List(Of ReservationHistoryInfo)()

            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT
                            r.ConfirmationCode,
                            g.FullName,
                            rm.RoomNumber,
                            rm.Type,
                            r.CheckIn,
                            r.CheckOut,
                            r.Guests,
                            r.AdultGuests,
                            r.ChildGuests,
                            r.FreeChildGuests,
                            r.Status,
                            p.Amount,
                            r.CreatedAt
                        FROM Reservations r
                        INNER JOIN Guests g ON g.Id = r.GuestId
                        INNER JOIN Rooms rm ON rm.Id = r.RoomId
                        INNER JOIN Payments p ON p.ReservationId = r.Id"
                    If accountId.HasValue Then
                        cmd.CommandText &= " WHERE " & ReservationOwnedByUserClause()
                        AddAccountFilterParameters(cmd, accountId.Value, guestEmail)
                    ElseIf Not String.IsNullOrWhiteSpace(guestEmail) Then
                        cmd.CommandText &= " WHERE g.Email = @GuestEmail COLLATE NOCASE"
                        cmd.Parameters.AddWithValue("@GuestEmail", guestEmail.Trim())
                    End If
                    cmd.CommandText &= " ORDER BY r.CreatedAt DESC"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            history.Add(New ReservationHistoryInfo With {
                                .ConfirmationCode = reader.GetString(0),
                                .GuestName = reader.GetString(1),
                                .RoomNumber = reader.GetString(2),
                                .RoomType = reader.GetString(3),
                                .CheckIn = ParseDate(reader.GetString(4)),
                                .CheckOut = ParseDate(reader.GetString(5)),
                                .Guests = reader.GetInt32(6),
                                .AdultGuests = reader.GetInt32(7),
                                .ChildGuests = reader.GetInt32(8),
                                .FreeChildGuests = reader.GetInt32(9),
                                .Status = reader.GetString(10),
                                .Total = reader.GetDecimal(11),
                                .CreatedAt = ParseDateTime(reader.GetString(12))
                            })
                        End While
                    End Using
                End Using
            End Using

            Return history
        End Function

        Public Sub ConfirmReservation(confirmationCode As String)
            Using connection = OpenConnection()
                connection.Open()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim reservation = GetReservationSummary(connection, transaction, confirmationCode)
                        If reservation Is Nothing Then
                            Throw New ArgumentException("Please select a valid reservation to confirm.")
                        End If

                        Dim isInitialConfirm = String.Equals(reservation.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                        Dim isChangeConfirm = String.Equals(reservation.Status, "Change Pending", StringComparison.OrdinalIgnoreCase)
                        If Not isInitialConfirm AndAlso Not isChangeConfirm Then
                            Throw New InvalidOperationException("Only pending or change-pending reservations can be confirmed.")
                        End If

                        Using cmd = connection.CreateCommand()
                            cmd.Transaction = transaction
                            cmd.CommandText = "UPDATE Reservations SET Status = 'Confirmed' WHERE Id = @Id"
                            cmd.Parameters.AddWithValue("@Id", reservation.Id)
                            cmd.ExecuteNonQuery()
                        End Using

                        Dim now = DateTime.UtcNow
                        If isChangeConfirm Then
                            QueueNotification(connection, transaction, reservation.Id, "Email", reservation.GuestEmail, "Reservation changes approved",
                                String.Format("Your reservation change request for {0} has been approved by the hotel admin. Your updated details are now confirmed.", confirmationCode), now)
                            If Not String.IsNullOrWhiteSpace(reservation.GuestPhone) Then
                                QueueNotification(connection, transaction, reservation.Id, "Alert", reservation.GuestPhone, "Changes approved",
                                    String.Format("Your reservation {0} changes have been approved by the hotel admin.", confirmationCode), now)
                            End If
                        Else
                            QueueNotification(connection, transaction, reservation.Id, "Email", reservation.GuestEmail, "Reservation confirmed by admin",
                                String.Format("Your reservation {0} has been confirmed by the hotel admin.", confirmationCode), now)
                            If Not String.IsNullOrWhiteSpace(reservation.GuestPhone) Then
                                QueueNotification(connection, transaction, reservation.Id, "Alert", reservation.GuestPhone, "Reservation confirmed",
                                    String.Format("Your reservation {0} has been confirmed by the hotel admin.", confirmationCode), now)
                            End If
                        End If

                        transaction.Commit()
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Sub

        Public Function GetReservationForEdit(confirmationCode As String, accountId As Integer, guestEmail As String) As ReservationDetailInfo
            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT
                            r.Id,
                            r.ConfirmationCode,
                            r.RoomId,
                            r.CheckIn,
                            r.CheckOut,
                            r.AdultGuests,
                            r.ChildGuests,
                            r.FreeChildGuests,
                            g.FullName,
                            g.Email,
                            g.Phone,
                            g.Address,
                            r.Notes,
                            r.Status,
                            p.Method,
                            p.Reference
                        FROM Reservations r
                        INNER JOIN Guests g ON g.Id = r.GuestId
                        INNER JOIN Payments p ON p.ReservationId = r.Id
                        WHERE r.ConfirmationCode = @ConfirmationCode COLLATE NOCASE
                          AND " & ReservationOwnedByUserClause()
                    AddAccountFilterParameters(cmd, accountId, guestEmail)
                    cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)

                    Using reader = cmd.ExecuteReader()
                        If Not reader.Read() Then
                            Return Nothing
                        End If

                        Dim reservationId = reader.GetInt32(0)
                        Dim detail As New ReservationDetailInfo With {
                            .ConfirmationCode = reader.GetString(1),
                            .RoomId = reader.GetInt32(2),
                            .CheckIn = ParseDate(reader.GetString(3)),
                            .CheckOut = ParseDate(reader.GetString(4)),
                            .AdultGuests = reader.GetInt32(5),
                            .ChildGuests = reader.GetInt32(6),
                            .FreeChildGuests = reader.GetInt32(7),
                            .GuestName = reader.GetString(8),
                            .Email = reader.GetString(9),
                            .Phone = reader.GetString(10),
                            .Address = GetStringOrEmpty(reader, 11),
                            .Notes = GetStringOrEmpty(reader, 12),
                            .Status = reader.GetString(13),
                            .PaymentMethod = reader.GetString(14),
                            .PaymentReference = GetStringOrEmpty(reader, 15)
                        }

                        detail.AddOns = GetReservationAddOnSelections(connection, reservationId)
                        Return detail
                    End Using
                End Using
            End Using
        End Function

        Public Function UpdateReservation(confirmationCode As String, accountId As Integer, guestEmail As String, request As ReservationInput) As ReceiptInfo
            ValidateReservation(request)

            Using connection = OpenConnection()
                connection.Open()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim existing = GetReservationSummary(connection, transaction, confirmationCode, accountId, guestEmail)
                        If existing Is Nothing Then
                            Throw New ArgumentException("Please select a valid reservation to update.")
                        End If

                        If Not String.Equals(existing.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) AndAlso
                           Not String.Equals(existing.Status, "Change Pending", StringComparison.OrdinalIgnoreCase) Then
                            Throw New InvalidOperationException("Only confirmed reservations can be updated.")
                        End If

                        Dim room = GetRoomRow(connection, transaction, request.RoomId)
                        If room Is Nothing Then
                            Throw New ArgumentException("Please select a valid room.")
                        End If

                        If room.Capacity < request.ChargeableGuests Then
                            Throw New ArgumentException(String.Format("Room {0} can only fit {1} guest(s).", room.RoomNumber, room.Capacity))
                        End If

                        If HasDateConflict(connection, request.RoomId, request.CheckIn, request.CheckOut, transaction, existing.Id) Then
                            Throw New InvalidOperationException("This room is already reserved for the selected dates.")
                        End If

                        Dim nights = Math.Max(1, CInt((request.CheckOut.Date - request.CheckIn.Date).TotalDays))
                        Dim roomSubtotal = nights * room.Rate
                        Dim selectedAddOns = BuildSelectedAddOns(connection, transaction, request.AddOns)
                        Dim addOnSubtotal = selectedAddOns.Sum(Function(addOn) addOn.Price * addOn.Quantity)
                        Dim total = roomSubtotal + addOnSubtotal
                        Dim now = DateTime.UtcNow

                        Using cmd = connection.CreateCommand()
                            cmd.Transaction = transaction
                            cmd.CommandText = "
                                UPDATE Guests
                                SET FullName = @FullName, Email = @Email, Phone = @Phone, Address = @Address
                                WHERE Id = @GuestId"
                            cmd.Parameters.AddWithValue("@FullName", request.GuestName.Trim())
                            cmd.Parameters.AddWithValue("@Email", request.Email.Trim())
                            cmd.Parameters.AddWithValue("@Phone", request.Phone.Trim())
                            cmd.Parameters.AddWithValue("@Address", If(String.IsNullOrWhiteSpace(request.Address), "", request.Address.Trim()))
                            cmd.Parameters.AddWithValue("@GuestId", existing.GuestId)
                            cmd.ExecuteNonQuery()
                        End Using

                        Using cmd = connection.CreateCommand()
                            cmd.Transaction = transaction
                            cmd.CommandText = "
                                UPDATE Reservations
                                SET RoomId = @RoomId,
                                    CheckIn = @CheckIn,
                                    CheckOut = @CheckOut,
                                    Guests = @Guests,
                                    AdultGuests = @AdultGuests,
                                    ChildGuests = @ChildGuests,
                                    FreeChildGuests = @FreeChildGuests,
                                    Notes = @Notes,
                                    Status = 'Change Pending'
                                WHERE Id = @Id"
                            cmd.Parameters.AddWithValue("@RoomId", request.RoomId)
                            cmd.Parameters.AddWithValue("@CheckIn", ToDate(request.CheckIn))
                            cmd.Parameters.AddWithValue("@CheckOut", ToDate(request.CheckOut))
                            cmd.Parameters.AddWithValue("@Guests", request.ChargeableGuests)
                            cmd.Parameters.AddWithValue("@AdultGuests", request.AdultGuests)
                            cmd.Parameters.AddWithValue("@ChildGuests", request.ChildGuests)
                            cmd.Parameters.AddWithValue("@FreeChildGuests", request.FreeChildGuests)
                            cmd.Parameters.AddWithValue("@Notes", If(String.IsNullOrWhiteSpace(request.Notes), "", request.Notes.Trim()))
                            cmd.Parameters.AddWithValue("@Id", existing.Id)
                            cmd.ExecuteNonQuery()
                        End Using

                        Using cmd = connection.CreateCommand()
                            cmd.Transaction = transaction
                            cmd.CommandText = "DELETE FROM ReservationAddOns WHERE ReservationId = @ReservationId"
                            cmd.Parameters.AddWithValue("@ReservationId", existing.Id)
                            cmd.ExecuteNonQuery()
                        End Using

                        For Each addOn In selectedAddOns
                            InsertReservationAddOn(connection, transaction, existing.Id, addOn.Id, addOn.Quantity)
                        Next

                        Using cmd = connection.CreateCommand()
                            cmd.Transaction = transaction
                            cmd.CommandText = "
                                UPDATE Payments
                                SET Method = @Method, Amount = @Amount, Reference = @Reference, PaidAt = @PaidAt
                                WHERE ReservationId = @ReservationId"
                            cmd.Parameters.AddWithValue("@Method", request.PaymentMethod.Trim())
                            cmd.Parameters.AddWithValue("@Amount", total)
                            cmd.Parameters.AddWithValue("@Reference", BuildPaymentReference(request.PaymentReference))
                            cmd.Parameters.AddWithValue("@PaidAt", ToDateTime(now))
                            cmd.Parameters.AddWithValue("@ReservationId", existing.Id)
                            cmd.ExecuteNonQuery()
                        End Using

                        QueueNotification(connection, transaction, existing.Id, "Email", request.Email.Trim(), "Reservation changes submitted",
                            String.Format("Your changes to reservation {0} have been submitted and are waiting for admin approval.", confirmationCode), now)
                        NotifyAdmins(connection, transaction, existing.Id, "Reservation change pending approval",
                            String.Format("Guest {0} updated reservation {1}. Please review and confirm the changes.", request.GuestName.Trim(), confirmationCode), now)

                        transaction.Commit()
                        Return GetReceipt(confirmationCode)
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Function GetNotifications(Optional recipientEmail As String = Nothing, Optional recipientPhone As String = Nothing) As List(Of NotificationInfo)
            Dim notifications As New List(Of NotificationInfo)()

            Using connection = OpenConnection()
                connection.Open()
                Using cmd = connection.CreateCommand()
                    cmd.CommandText = "
                        SELECT r.ConfirmationCode, n.Channel, n.Recipient, n.Subject, n.Message, n.Status, n.CreatedAt
                        FROM Notifications n
                        INNER JOIN Reservations r ON r.Id = n.ReservationId"
                    Dim filters As New List(Of String)()
                    If Not String.IsNullOrWhiteSpace(recipientEmail) Then
                        filters.Add("n.Recipient = @RecipientEmail COLLATE NOCASE")
                        cmd.Parameters.AddWithValue("@RecipientEmail", recipientEmail.Trim())
                    End If
                    If Not String.IsNullOrWhiteSpace(recipientPhone) Then
                        filters.Add("n.Recipient = @RecipientPhone")
                        cmd.Parameters.AddWithValue("@RecipientPhone", recipientPhone.Trim())
                    End If
                    If filters.Count > 0 Then
                        cmd.CommandText &= " WHERE (" & String.Join(" OR ", filters) & ")"
                    End If
                    cmd.CommandText &= " ORDER BY n.CreatedAt DESC LIMIT 30"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            notifications.Add(New NotificationInfo With {
                                .ConfirmationCode = reader.GetString(0),
                                .Channel = reader.GetString(1),
                                .Recipient = reader.GetString(2),
                                .Subject = reader.GetString(3),
                                .Message = reader.GetString(4),
                                .Status = reader.GetString(5),
                                .CreatedAt = ParseDateTime(reader.GetString(6))
                            })
                        End While
                    End Using
                End Using
            End Using

            Return notifications
        End Function

        Private Function OpenConnection() As SqliteConnection
            Return New SqliteConnection(_connectionString)
        End Function

        Private Sub EnsureDatabaseDirectory()
            Dim builder As New SqliteConnectionStringBuilder(_connectionString)
            Dim databaseDirectory = Path.GetDirectoryName(builder.DataSource)
            If Not String.IsNullOrEmpty(databaseDirectory) AndAlso Not Directory.Exists(databaseDirectory) Then
                Directory.CreateDirectory(databaseDirectory)
            End If
        End Sub

        Private Shared Sub EnsureSchemaUpgrades(connection As SqliteConnection)
            If Not ColumnExists(connection, "Reservations", "AccountId") Then
                ExecuteSeed(connection, "ALTER TABLE Reservations ADD COLUMN AccountId INTEGER;")
            End If

            ExecuteSeed(connection, "
                UPDATE Reservations
                SET AccountId = (
                    SELECT a.Id
                    FROM Accounts a
                    INNER JOIN Guests g ON g.Id = Reservations.GuestId
                    WHERE a.Email = g.Email COLLATE NOCASE
                      AND a.Role = 'User'
                    LIMIT 1
                )
                WHERE AccountId IS NULL;")
        End Sub

        Private Shared Function ColumnExists(connection As SqliteConnection, tableName As String, columnName As String) As Boolean
            Using cmd = connection.CreateCommand()
                cmd.CommandText = $"PRAGMA table_info({tableName});"
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        If String.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase) Then
                            Return True
                        End If
                    End While
                End Using
            End Using

            Return False
        End Function

        Private Shared Function ReservationOwnedByUserClause() As String
            Return "(r.AccountId = @AccountId OR (r.AccountId IS NULL AND g.Email = @GuestEmail COLLATE NOCASE))"
        End Function

        Private Shared Sub AddAccountFilterParameters(cmd As SqliteCommand, accountId As Integer, guestEmail As String)
            cmd.Parameters.AddWithValue("@AccountId", accountId)
            cmd.Parameters.AddWithValue("@GuestEmail", If(String.IsNullOrWhiteSpace(guestEmail), "", guestEmail.Trim()))
        End Sub

        Private Shared Function ReservationOwnedByUser(connection As SqliteConnection, confirmationCode As String, accountId As Integer, guestEmail As String) As Boolean
            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
                    SELECT COUNT(*)
                    FROM Reservations r
                    INNER JOIN Guests g ON g.Id = r.GuestId
                    WHERE r.ConfirmationCode = @ConfirmationCode COLLATE NOCASE
                      AND " & ReservationOwnedByUserClause()
                AddAccountFilterParameters(cmd, accountId, guestEmail)
                cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)
                Return CInt(cmd.ExecuteScalar()) > 0
            End Using
        End Function

        Private Shared Sub CreateSchema(connection As SqliteConnection)
            connection.Open()
            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
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
                    );"
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function GetRowCount(connection As SqliteConnection, tableName As String) As Integer
            Using cmd = connection.CreateCommand()
                cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}"
                Return CInt(cmd.ExecuteScalar())
            End Using
        End Function

        Private Shared Sub SeedRooms(connection As SqliteConnection)
            ExecuteSeed(connection, "
                INSERT INTO Rooms (RoomNumber, Type, Capacity, Rate, Amenities, Status) VALUES
                ('101', 'Cozy Standard', 2, 3200, 'Queen bed, garden view, Wi-Fi, hot shower', 'Available'),
                ('102', 'Garden Twin', 3, 3800, 'Twin beds, patio, Wi-Fi, coffee nook', 'Available'),
                ('201', 'Deluxe Queen', 4, 5200, 'Queen bed, lounge chair, smart TV, minibar', 'Available'),
                ('202', 'Sunset Suite', 4, 7200, 'King bed, balcony, bathtub, breakfast set', 'Available'),
                ('301', 'Family Loft', 6, 9800, 'Two bedrooms, kitchenette, dining area, board games', 'Available'),
                ('302', 'Premier Suite', 2, 11800, 'King bed, private balcony, bath ritual kit, late checkout', 'Available');")
        End Sub

        Private Shared Sub SeedAddOns(connection As SqliteConnection)
            ExecuteSeed(connection, "
                INSERT INTO AddOns (Name, Description, Price) VALUES
                ('Breakfast Tray', 'Warm breakfast served to the room.', 650),
                ('Airport Pickup', 'Private car pickup for arriving guests.', 1800),
                ('Spa Welcome Kit', 'Bath salts, candle, and herbal tea.', 950),
                ('Extra Bed', 'Foldable bed with linen setup.', 1200),
                ('Pet Care Pack', 'Pet mat, bowl, and welcome treat.', 750);")
        End Sub

        Private Shared Sub SeedAdminAccount(connection As SqliteConnection)
            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
                    INSERT INTO Accounts (FullName, Email, Phone, Username, PasswordHash, Role, CreatedAt)
                    VALUES (@FullName, @Email, @Phone, @Username, @PasswordHash, @Role, @CreatedAt)"
                cmd.Parameters.AddWithValue("@FullName", "System Administrator")
                cmd.Parameters.AddWithValue("@Email", "admin@casareserve.local")
                cmd.Parameters.AddWithValue("@Phone", "0000000000")
                cmd.Parameters.AddWithValue("@Username", "admin")
                cmd.Parameters.AddWithValue("@PasswordHash", HashPassword("admin123"))
                cmd.Parameters.AddWithValue("@Role", "Admin")
                cmd.Parameters.AddWithValue("@CreatedAt", ToDateTime(DateTime.UtcNow))
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Sub ExecuteSeed(connection As SqliteConnection, sql As String)
            Using cmd = connection.CreateCommand()
                cmd.CommandText = sql
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function AccountExists(connection As SqliteConnection, username As String) As Boolean
            Using cmd = connection.CreateCommand()
                cmd.CommandText = "SELECT COUNT(*) FROM Accounts WHERE Username = @Username COLLATE NOCASE"
                cmd.Parameters.AddWithValue("@Username", username.Trim())
                Return CInt(cmd.ExecuteScalar()) > 0
            End Using
        End Function

        Private Shared Function GetAccountById(connection As SqliteConnection, accountId As Integer) As AccountInfo
            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
                    SELECT Id, FullName, Email, Phone, Username, Role, CreatedAt
                    FROM Accounts
                    WHERE Id = @Id"
                cmd.Parameters.AddWithValue("@Id", accountId)
                Using reader = cmd.ExecuteReader()
                    If Not reader.Read() Then
                        Throw New InvalidOperationException("Account could not be loaded.")
                    End If

                    Return ReadAccountInfo(reader)
                End Using
            End Using
        End Function

        Private Shared Function ReadAccountInfo(reader As SqliteDataReader) As AccountInfo
            Return New AccountInfo With {
                .Id = reader.GetInt32(0),
                .FullName = reader.GetString(1),
                .Email = reader.GetString(2),
                .Phone = GetStringOrEmpty(reader, 3),
                .Username = reader.GetString(4),
                .Role = reader.GetString(5),
                .CreatedAt = ParseDateTime(reader.GetString(6))
            }
        End Function

        Private Shared Function HasDateConflict(connection As SqliteConnection, roomId As Integer, checkIn As Date, checkOut As Date, Optional transaction As SqliteTransaction = Nothing, Optional excludeReservationId As Integer? = Nothing) As Boolean
            Using cmd = connection.CreateCommand()
                If transaction IsNot Nothing Then
                    cmd.Transaction = transaction
                End If

                cmd.CommandText = "
                    SELECT COUNT(*)
                    FROM Reservations
                    WHERE RoomId = @RoomId
                      AND Status IN ('Pending', 'Confirmed', 'Change Pending', 'Reserved', 'Checked In')
                      AND CheckIn < @CheckOut
                      AND CheckOut > @CheckIn"
                If excludeReservationId.HasValue Then
                    cmd.CommandText &= " AND Id <> @ExcludeReservationId"
                    cmd.Parameters.AddWithValue("@ExcludeReservationId", excludeReservationId.Value)
                End If
                cmd.Parameters.AddWithValue("@RoomId", roomId)
                cmd.Parameters.AddWithValue("@CheckIn", ToDate(checkIn))
                cmd.Parameters.AddWithValue("@CheckOut", ToDate(checkOut))
                Return CInt(cmd.ExecuteScalar()) > 0
            End Using
        End Function

        Private Shared Function GetRoomRow(connection As SqliteConnection, transaction As SqliteTransaction, roomId As Integer) As RoomRow
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "SELECT RoomNumber, Capacity, Rate FROM Rooms WHERE Id = @Id"
                cmd.Parameters.AddWithValue("@Id", roomId)
                Using reader = cmd.ExecuteReader()
                    If Not reader.Read() Then
                        Return Nothing
                    End If

                    Return New RoomRow With {
                        .RoomNumber = reader.GetString(0),
                        .Capacity = reader.GetInt32(1),
                        .Rate = reader.GetDecimal(2)
                    }
                End Using
            End Using
        End Function

        Private Shared Function BuildSelectedAddOns(connection As SqliteConnection, transaction As SqliteTransaction, selected As List(Of SelectedAddOn)) As List(Of SelectedAddOnDetail)
            Dim details As New List(Of SelectedAddOnDetail)()
            Dim grouped = selected.Where(Function(item) item.Quantity > 0).
                GroupBy(Function(item) item.AddOnId).
                Select(Function(group) New SelectedAddOn With {.AddOnId = group.Key, .Quantity = group.Sum(Function(item) item.Quantity)})

            For Each addOn In grouped
                Using cmd = connection.CreateCommand()
                    cmd.Transaction = transaction
                    cmd.CommandText = "SELECT Id, Price FROM AddOns WHERE Id = @Id"
                    cmd.Parameters.AddWithValue("@Id", addOn.AddOnId)
                    Using reader = cmd.ExecuteReader()
                        If Not reader.Read() Then
                            Throw New ArgumentException("One selected add-on is no longer available.")
                        End If

                        details.Add(New SelectedAddOnDetail With {
                            .Id = reader.GetInt32(0),
                            .Quantity = addOn.Quantity,
                            .Price = reader.GetDecimal(1)
                        })
                    End Using
                End Using
            Next

            Return details
        End Function

        Private Shared Function InsertGuest(connection As SqliteConnection, transaction As SqliteTransaction, request As ReservationInput, createdAt As Date) As Integer
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    INSERT INTO Guests (FullName, Email, Phone, Address, CreatedAt)
                    VALUES (@FullName, @Email, @Phone, @Address, @CreatedAt);
                    SELECT last_insert_rowid();"
                cmd.Parameters.AddWithValue("@FullName", request.GuestName.Trim())
                cmd.Parameters.AddWithValue("@Email", request.Email.Trim())
                cmd.Parameters.AddWithValue("@Phone", request.Phone.Trim())
                cmd.Parameters.AddWithValue("@Address", If(String.IsNullOrWhiteSpace(request.Address), "", request.Address.Trim()))
                cmd.Parameters.AddWithValue("@CreatedAt", ToDateTime(createdAt))
                Return CInt(cmd.ExecuteScalar())
            End Using
        End Function

        Private Shared Function InsertReservation(connection As SqliteConnection, transaction As SqliteTransaction, request As ReservationInput, confirmationCode As String, guestId As Integer, createdAt As Date) As Integer
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    INSERT INTO Reservations (
                        ConfirmationCode, RoomId, GuestId, CheckIn, CheckOut, Guests,
                        AdultGuests, ChildGuests, FreeChildGuests, Status, Notes, CreatedAt, AccountId)
                    VALUES (
                        @ConfirmationCode, @RoomId, @GuestId, @CheckIn, @CheckOut, @Guests,
                        @AdultGuests, @ChildGuests, @FreeChildGuests, @Status, @Notes, @CreatedAt, @AccountId);
                    SELECT last_insert_rowid();"
                cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)
                cmd.Parameters.AddWithValue("@RoomId", request.RoomId)
                cmd.Parameters.AddWithValue("@GuestId", guestId)
                cmd.Parameters.AddWithValue("@CheckIn", ToDate(request.CheckIn))
                cmd.Parameters.AddWithValue("@CheckOut", ToDate(request.CheckOut))
                cmd.Parameters.AddWithValue("@Guests", request.ChargeableGuests)
                cmd.Parameters.AddWithValue("@AdultGuests", request.AdultGuests)
                cmd.Parameters.AddWithValue("@ChildGuests", request.ChildGuests)
                cmd.Parameters.AddWithValue("@FreeChildGuests", request.FreeChildGuests)
                cmd.Parameters.AddWithValue("@Status", "Pending")
                cmd.Parameters.AddWithValue("@Notes", If(String.IsNullOrWhiteSpace(request.Notes), "", request.Notes.Trim()))
                cmd.Parameters.AddWithValue("@CreatedAt", ToDateTime(createdAt))
                cmd.Parameters.AddWithValue("@AccountId", If(request.AccountId > 0, CObj(request.AccountId), DBNull.Value))
                Return CInt(cmd.ExecuteScalar())
            End Using
        End Function

        Private Shared Sub InsertReservationAddOn(connection As SqliteConnection, transaction As SqliteTransaction, reservationId As Integer, addOnId As Integer, quantity As Integer)
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    INSERT INTO ReservationAddOns (ReservationId, AddOnId, Quantity)
                    VALUES (@ReservationId, @AddOnId, @Quantity)"
                cmd.Parameters.AddWithValue("@ReservationId", reservationId)
                cmd.Parameters.AddWithValue("@AddOnId", addOnId)
                cmd.Parameters.AddWithValue("@Quantity", quantity)
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Sub InsertPayment(connection As SqliteConnection, transaction As SqliteTransaction, reservationId As Integer, request As ReservationInput, total As Decimal, paidAt As Date)
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    INSERT INTO Payments (ReservationId, Method, Amount, Status, PaidAt, Reference)
                    VALUES (@ReservationId, @Method, @Amount, @Status, @PaidAt, @Reference)"
                cmd.Parameters.AddWithValue("@ReservationId", reservationId)
                cmd.Parameters.AddWithValue("@Method", request.PaymentMethod.Trim())
                cmd.Parameters.AddWithValue("@Amount", total)
                cmd.Parameters.AddWithValue("@Status", "Paid")
                cmd.Parameters.AddWithValue("@PaidAt", ToDateTime(paidAt))
                cmd.Parameters.AddWithValue("@Reference", BuildPaymentReference(request.PaymentReference))
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function GetReceiptAddOns(connection As SqliteConnection, reservationId As Integer) As List(Of AddOnLine)
            Dim lines As New List(Of AddOnLine)()

            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
                    SELECT a.Name, ra.Quantity, a.Price
                    FROM ReservationAddOns ra
                    INNER JOIN AddOns a ON a.Id = ra.AddOnId
                    WHERE ra.ReservationId = @ReservationId
                    ORDER BY a.Name"
                cmd.Parameters.AddWithValue("@ReservationId", reservationId)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim quantity = reader.GetInt32(1)
                        Dim unitPrice = reader.GetDecimal(2)
                        lines.Add(New AddOnLine With {
                            .Name = reader.GetString(0),
                            .Quantity = quantity,
                            .UnitPrice = unitPrice,
                            .Total = unitPrice * quantity
                        })
                    End While
                End Using
            End Using

            Return lines
        End Function

        Private Shared Function GetReservationSummary(connection As SqliteConnection, transaction As SqliteTransaction, confirmationCode As String, Optional accountId As Integer? = Nothing, Optional guestEmail As String = Nothing) As ReservationSummary
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    SELECT r.Id, r.Status, g.Email, g.Phone, g.Id
                    FROM Reservations r
                    INNER JOIN Guests g ON g.Id = r.GuestId
                    WHERE r.ConfirmationCode = @ConfirmationCode COLLATE NOCASE"
                If accountId.HasValue Then
                    cmd.CommandText &= " AND " & ReservationOwnedByUserClause()
                    AddAccountFilterParameters(cmd, accountId.Value, guestEmail)
                ElseIf Not String.IsNullOrWhiteSpace(guestEmail) Then
                    cmd.CommandText &= " AND g.Email = @GuestEmail COLLATE NOCASE"
                    cmd.Parameters.AddWithValue("@GuestEmail", guestEmail.Trim())
                End If
                cmd.Parameters.AddWithValue("@ConfirmationCode", confirmationCode)
                Using reader = cmd.ExecuteReader()
                    If Not reader.Read() Then
                        Return Nothing
                    End If

                    Return New ReservationSummary With {
                        .Id = reader.GetInt32(0),
                        .Status = reader.GetString(1),
                        .GuestEmail = reader.GetString(2),
                        .GuestPhone = GetStringOrEmpty(reader, 3),
                        .GuestId = reader.GetInt32(4)
                    }
                End Using
            End Using
        End Function

        Private Shared Function GetReservationAddOnSelections(connection As SqliteConnection, reservationId As Integer) As List(Of SelectedAddOn)
            Dim selections As New List(Of SelectedAddOn)()

            Using cmd = connection.CreateCommand()
                cmd.CommandText = "
                    SELECT AddOnId, Quantity
                    FROM ReservationAddOns
                    WHERE ReservationId = @ReservationId"
                cmd.Parameters.AddWithValue("@ReservationId", reservationId)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        selections.Add(New SelectedAddOn With {
                            .AddOnId = reader.GetInt32(0),
                            .Quantity = reader.GetInt32(1)
                        })
                    End While
                End Using
            End Using

            Return selections
        End Function

        Private Shared Sub NotifyAdmins(connection As SqliteConnection, transaction As SqliteTransaction, reservationId As Integer, subject As String, message As String, createdAt As Date)
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "SELECT Email FROM Accounts WHERE Role = 'Admin'"
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        QueueNotification(connection, transaction, reservationId, "Admin", reader.GetString(0), subject, message, createdAt)
                    End While
                End Using
            End Using
        End Sub

        Private Shared Sub QueueNotification(connection As SqliteConnection, transaction As SqliteTransaction, reservationId As Integer, channel As String, recipient As String, subject As String, message As String, createdAt As Date)
            Using cmd = connection.CreateCommand()
                cmd.Transaction = transaction
                cmd.CommandText = "
                    INSERT INTO Notifications (ReservationId, Channel, Recipient, Subject, Message, Status, CreatedAt)
                    VALUES (@ReservationId, @Channel, @Recipient, @Subject, @Message, @Status, @CreatedAt)"
                cmd.Parameters.AddWithValue("@ReservationId", reservationId)
                cmd.Parameters.AddWithValue("@Channel", channel)
                cmd.Parameters.AddWithValue("@Recipient", recipient)
                cmd.Parameters.AddWithValue("@Subject", subject)
                cmd.Parameters.AddWithValue("@Message", message)
                cmd.Parameters.AddWithValue("@Status", "Queued locally")
                cmd.Parameters.AddWithValue("@CreatedAt", ToDateTime(createdAt))
                cmd.ExecuteNonQuery()
            End Using
        End Sub

        Private Function GenerateConfirmationCode(connection As SqliteConnection, transaction As SqliteTransaction) As String
            For attempt As Integer = 1 To 20
                Dim code = String.Format("HR-{0:yyMMdd}-{1}", DateTime.UtcNow, _random.Next(1000, 9999))
                Using cmd = connection.CreateCommand()
                    cmd.Transaction = transaction
                    cmd.CommandText = "SELECT COUNT(*) FROM Reservations WHERE ConfirmationCode = @ConfirmationCode COLLATE NOCASE"
                    cmd.Parameters.AddWithValue("@ConfirmationCode", code)
                    If CInt(cmd.ExecuteScalar()) = 0 Then
                        Return code
                    End If
                End Using
            Next

            Throw New InvalidOperationException("Could not generate a reservation code. Please try again.")
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

        Private Shared Function HashPassword(password As String) As String
            Using sha = SHA256.Create()
                Dim bytes = Encoding.UTF8.GetBytes(password)
                Dim hash = sha.ComputeHash(bytes)
                Return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()
            End Using
        End Function

        Private Shared Function GetStringOrEmpty(reader As SqliteDataReader, ordinal As Integer) As String
            Return If(reader.IsDBNull(ordinal), "", reader.GetString(ordinal))
        End Function

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

        Private Class RoomRow
            Public Property RoomNumber As String = ""
            Public Property Capacity As Integer
            Public Property Rate As Decimal
        End Class

        Private Class ReservationSummary
            Public Property Id As Integer
            Public Property Status As String = ""
            Public Property GuestEmail As String = ""
            Public Property GuestPhone As String = ""
            Public Property GuestId As Integer
        End Class
    End Class
End Namespace

Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace HotelReservation
    Public Class AdminForm
        Inherits Form

        Private ReadOnly _repository As HotelRepository
        Private ReadOnly _account As AccountInfo
        Private ReadOnly _cream As Color = Color.FromArgb(247, 239, 227)
        Private ReadOnly _linen As Color = Color.FromArgb(255, 250, 241)
        Private ReadOnly _coffee As Color = Color.FromArgb(106, 73, 52)
        Private ReadOnly _espresso As Color = Color.FromArgb(53, 35, 24)
        Private ReadOnly _muted As Color = Color.FromArgb(125, 102, 83)

        Private _roomsList As ListView
        Private _reservationsList As ListView
        Private _accountsList As ListView
        Private _notificationsList As ListView

        Public Property LogoutRequested As Boolean

        Public Sub New(repository As HotelRepository, account As AccountInfo)
            _repository = repository
            _account = account
            ConfigureWindow()
            BuildLayout()
            RefreshDashboard()
        End Sub

        Private Sub ConfigureWindow()
            Text = "Casa Reserve - Admin Dashboard"
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(1080, 720)
            Size = New Size(1160, 780)
            BackColor = _cream
            Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)
        End Sub

        Private Sub BuildLayout()
            Dim root = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(18),
                .BackColor = _cream
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 108))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(root)

            Dim header = New Panel With {.Dock = DockStyle.Fill, .BackColor = _cream}
            Dim title = New Label With {
                .Text = "Admin Dashboard",
                .Dock = DockStyle.Top,
                .Height = 48,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 26.0F, FontStyle.Bold, GraphicsUnit.Point)
            }
            Dim subtitle = New Label With {
                .Text = String.Format("Signed in as {0}. Manage rooms, bookings, accounts, and notifications.", _account.FullName),
                .Dock = DockStyle.Top,
                .Height = 28,
                .ForeColor = _muted
            }
            Dim refreshButton = MakeButton("Refresh")
            refreshButton.Width = 120
            refreshButton.Height = 34
            refreshButton.Location = New Point(0, 76)
            AddHandler refreshButton.Click, AddressOf RefreshClicked

            Dim logoutButton = MakeButton("Log out")
            logoutButton.Width = 120
            logoutButton.Height = 34
            logoutButton.Location = New Point(130, 76)
            AddHandler logoutButton.Click, AddressOf LogoutClicked

            header.Controls.Add(logoutButton)
            header.Controls.Add(refreshButton)
            header.Controls.Add(subtitle)
            header.Controls.Add(title)
            root.Controls.Add(header, 0, 0)

            Dim tabs = New TabControl With {.Dock = DockStyle.Fill}
            root.Controls.Add(tabs, 0, 1)

            Dim reservationsTab = New TabPage("Reservations") With {.BackColor = _linen, .Padding = New Padding(12)}
            Dim roomsTab = New TabPage("Rooms") With {.BackColor = _linen, .Padding = New Padding(12)}
            Dim accountsTab = New TabPage("Accounts") With {.BackColor = _linen, .Padding = New Padding(12)}
            Dim notificationsTab = New TabPage("Notifications") With {.BackColor = _linen, .Padding = New Padding(12)}
            tabs.TabPages.Add(reservationsTab)
            tabs.TabPages.Add(roomsTab)
            tabs.TabPages.Add(accountsTab)
            tabs.TabPages.Add(notificationsTab)

            _reservationsList = CreateListView()
            _reservationsList.Columns.Add("Code", 130)
            _reservationsList.Columns.Add("Guest", 180)
            _reservationsList.Columns.Add("Room", 170)
            _reservationsList.Columns.Add("Dates", 220)
            _reservationsList.Columns.Add("Guests", 80)
            _reservationsList.Columns.Add("Total", 120)
            _reservationsList.Columns.Add("Status", 120)
            reservationsTab.Controls.Add(_reservationsList)

            _roomsList = CreateListView()
            _roomsList.Columns.Add("Room", 100)
            _roomsList.Columns.Add("Type", 180)
            _roomsList.Columns.Add("Capacity", 90)
            _roomsList.Columns.Add("Rate", 120)
            _roomsList.Columns.Add("Status", 120)
            _roomsList.Columns.Add("Amenities", 520)
            roomsTab.Controls.Add(_roomsList)

            _accountsList = CreateListView()
            _accountsList.Columns.Add("Name", 220)
            _accountsList.Columns.Add("Username", 150)
            _accountsList.Columns.Add("Role", 100)
            _accountsList.Columns.Add("Email", 230)
            _accountsList.Columns.Add("Phone", 150)
            _accountsList.Columns.Add("Created", 170)
            accountsTab.Controls.Add(_accountsList)

            _notificationsList = CreateListView()
            _notificationsList.Columns.Add("Code", 120)
            _notificationsList.Columns.Add("Channel", 90)
            _notificationsList.Columns.Add("Recipient", 180)
            _notificationsList.Columns.Add("Subject", 220)
            _notificationsList.Columns.Add("Message", 440)
            _notificationsList.Columns.Add("Status", 120)
            notificationsTab.Controls.Add(_notificationsList)
        End Sub

        Private Sub RefreshClicked(sender As Object, e As EventArgs)
            RefreshDashboard()
        End Sub

        Private Sub LogoutClicked(sender As Object, e As EventArgs)
            LogoutRequested = True
            Close()
        End Sub

        Private Sub RefreshDashboard()
            RefreshRooms()
            RefreshReservations()
            RefreshAccounts()
            RefreshNotifications()
        End Sub

        Private Sub RefreshRooms()
            _roomsList.Items.Clear()
            For Each room In _repository.GetRooms()
                Dim row = New ListViewItem(room.RoomNumber)
                row.SubItems.Add(room.RoomType)
                row.SubItems.Add(room.Capacity.ToString())
                row.SubItems.Add(String.Format("{0:C2}", room.Rate))
                row.SubItems.Add(room.Status)
                row.SubItems.Add(room.Amenities)
                _roomsList.Items.Add(row)
            Next
        End Sub

        Private Sub RefreshReservations()
            _reservationsList.Items.Clear()
            For Each reservation In _repository.GetReservationHistory()
                Dim row = New ListViewItem(reservation.ConfirmationCode)
                row.SubItems.Add(reservation.GuestName)
                row.SubItems.Add(String.Format("{0} {1}", reservation.RoomNumber, reservation.RoomType))
                row.SubItems.Add(String.Format("{0:MMM dd, yyyy} - {1:MMM dd, yyyy}", reservation.CheckIn, reservation.CheckOut))
                row.SubItems.Add(reservation.Guests.ToString())
                row.SubItems.Add(String.Format("{0:C2}", reservation.Total))
                row.SubItems.Add(reservation.Status)
                _reservationsList.Items.Add(row)
            Next
        End Sub

        Private Sub RefreshAccounts()
            _accountsList.Items.Clear()
            For Each account In _repository.GetAccounts()
                Dim row = New ListViewItem(account.FullName)
                row.SubItems.Add(account.Username)
                row.SubItems.Add(account.Role)
                row.SubItems.Add(account.Email)
                row.SubItems.Add(account.Phone)
                row.SubItems.Add(String.Format("{0:MMM dd, yyyy hh:mm tt}", account.CreatedAt))
                _accountsList.Items.Add(row)
            Next
        End Sub

        Private Sub RefreshNotifications()
            _notificationsList.Items.Clear()
            For Each notification In _repository.GetNotifications()
                Dim row = New ListViewItem(notification.ConfirmationCode)
                row.SubItems.Add(notification.Channel)
                row.SubItems.Add(notification.Recipient)
                row.SubItems.Add(notification.Subject)
                row.SubItems.Add(notification.Message)
                row.SubItems.Add(notification.Status)
                _notificationsList.Items.Add(row)
            Next
        End Sub

        Private Function CreateListView() As ListView
            Return New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .FullRowSelect = True,
                .GridLines = False,
                .BackColor = _linen,
                .ForeColor = _espresso
            }
        End Function

        Private Function MakeButton(text As String) As Button
            Return New Button With {
                .Text = text,
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat
            }
        End Function
    End Class
End Namespace

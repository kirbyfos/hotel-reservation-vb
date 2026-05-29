Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Namespace HotelReservation
    Public Class MainForm
        Inherits Form

        Private ReadOnly _repository As HotelRepository
        Private ReadOnly _account As AccountInfo
        Private ReadOnly _rooms As New List(Of RoomInfo)()
        Private ReadOnly _addOns As New List(Of AddOnInfo)()
        Private ReadOnly _addOnInputs As New Dictionary(Of Integer, NumericUpDown)()
        Private ReadOnly _addOnChecks As New Dictionary(Of Integer, CheckBox)()
        Private _latestReceipt As ReceiptInfo
        Private _reserveButton As Button
        Private _historyRefreshButton As Button
        Private _editHistoryButton As Button
        Private _printHistoryButton As Button
        Private _notificationRefreshButton As Button
        Private _tabs As TabControl

        Private ReadOnly _cream As Color = Color.FromArgb(247, 239, 227)
        Private ReadOnly _linen As Color = Color.FromArgb(255, 250, 241)
        Private ReadOnly _sand As Color = Color.FromArgb(234, 216, 191)
        Private ReadOnly _coffee As Color = Color.FromArgb(106, 73, 52)
        Private ReadOnly _espresso As Color = Color.FromArgb(53, 35, 24)
        Private ReadOnly _muted As Color = Color.FromArgb(125, 102, 83)
        Private ReadOnly _green As Color = Color.FromArgb(111, 125, 72)
        Private ReadOnly _red As Color = Color.FromArgb(154, 78, 63)

        Private _checkInPicker As DateTimePicker
        Private _checkOutPicker As DateTimePicker
        Private _availabilityAdults As NumericUpDown
        Private _availabilityChildren As NumericUpDown
        Private _availabilityFreeChildren As NumericUpDown
        Private _availabilitySearchText As TextBox
        Private _availabilityLabel As Label
        Private _roomsPanel As FlowLayoutPanel
        Private _roomCombo As ComboBox
        Private _bookingCheckInPicker As DateTimePicker
        Private _bookingCheckOutPicker As DateTimePicker
        Private _bookingAdults As NumericUpDown
        Private _bookingChildren As NumericUpDown
        Private _bookingFreeChildren As NumericUpDown
        Private _guestNameText As TextBox
        Private _emailText As TextBox
        Private _phoneText As TextBox
        Private _addressText As TextBox
        Private _paymentCombo As ComboBox
        Private _paymentReferenceText As TextBox
        Private _notesText As TextBox
        Private _addOnsPanel As FlowLayoutPanel
        Private _totalLabel As Label
        Private _receiptBox As RichTextBox
        Private _historyList As ListView
        Private _historyDetailsBox As RichTextBox
        Private _viewHistoryButton As Button
        Private _notificationList As ListView

        Public Property LogoutRequested As Boolean

        Public Sub New(repository As HotelRepository, account As AccountInfo)
            _repository = repository
            _account = account
            ConfigureWindow()
            BuildLayout()
            LoadInitialData()
        End Sub

        Private Sub ConfigureWindow()
            Text = "Casa Reserve - User Booking"
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(1120, 760)
            Size = New Size(1220, 820)
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
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 116))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(root)

            root.Controls.Add(BuildHeader(), 0, 0)

            Dim tabs = New TabControl With {
                .Dock = DockStyle.Fill,
                .Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)
            }
            _tabs = tabs
            AddHandler tabs.SelectedIndexChanged, AddressOf TabChanged
            root.Controls.Add(tabs, 0, 1)

            Dim bookingTab = New TabPage("Booking") With {.BackColor = _cream, .Padding = New Padding(10)}
            Dim historyTab = New TabPage("Reserve History") With {.BackColor = _cream, .Padding = New Padding(10)}
            Dim notificationTab = New TabPage("Email & Alerts") With {.BackColor = _cream, .Padding = New Padding(10)}
            tabs.TabPages.Add(bookingTab)
            tabs.TabPages.Add(historyTab)
            tabs.TabPages.Add(notificationTab)

            bookingTab.Controls.Add(BuildBookingPage())
            historyTab.Controls.Add(BuildHistoryPage())
            notificationTab.Controls.Add(BuildNotificationPage())
        End Sub

        Private Function BuildHeader() As Control
            Dim panel = New Panel With {.Dock = DockStyle.Fill, .BackColor = _cream}

            Dim title = New Label With {
                .Text = "Casa Reserve",
                .AutoSize = False,
                .Dock = DockStyle.Top,
                .Height = 52,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 28.0F, FontStyle.Bold, GraphicsUnit.Point)
            }

            Dim subtitle = New Label With {
                .Text = String.Format("Signed in as {0}. Book a hotel room, choose add-ons, pay, and print your receipt.", _account.FullName),
                .AutoSize = False,
                .Dock = DockStyle.Top,
                .Height = 34,
                .ForeColor = _muted,
                .Font = New Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point)
            }

            Dim logoutButton = MakeButton("Log out")
            logoutButton.Size = New Size(110, 34)
            logoutButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            logoutButton.Location = New Point(panel.Width - logoutButton.Width - 4, 8)
            AddHandler logoutButton.Click, AddressOf LogoutClicked

            panel.Controls.Add(logoutButton)
            panel.Controls.Add(subtitle)
            panel.Controls.Add(title)
            AddHandler panel.Resize, Sub(sender, e)
                                         logoutButton.Location = New Point(panel.ClientSize.Width - logoutButton.Width - 4, 8)
                                     End Sub
            Return panel
        End Function

        Private Function BuildBookingPage() As Control
            Dim split = New SplitContainer With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical,
                .SplitterDistance = 470,
                .BackColor = _cream
            }

            split.Panel1.Controls.Add(BuildRoomAvailabilityPanel())
            split.Panel2.Controls.Add(BuildReservationPanel())
            Return split
        End Function

        Private Function BuildRoomAvailabilityPanel() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill

            Dim title = MakeTitle("Room Availability Checker")
            panel.Controls.Add(title)

            Dim inputs = New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .ColumnCount = 2,
                .Height = 282,
                .Padding = New Padding(0, 8, 0, 0)
            }
            inputs.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            inputs.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            panel.Controls.Add(inputs)

            _checkInPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _checkOutPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _availabilityAdults = New NumericUpDown With {.Minimum = 1, .Maximum = 20, .Value = 2, .Dock = DockStyle.Fill}
            _availabilityChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}
            _availabilityFreeChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}
            _availabilitySearchText = New TextBox With {.Dock = DockStyle.Fill}
            AddHandler _availabilitySearchText.TextChanged, AddressOf AvailabilitySearchChanged

            inputs.Controls.Add(WrapField("Check-in", _checkInPicker), 0, 0)
            inputs.Controls.Add(WrapField("Check-out", _checkOutPicker), 1, 0)
            inputs.Controls.Add(WrapField("Adults", _availabilityAdults), 0, 1)
            inputs.Controls.Add(WrapField("Children 4+ years old", _availabilityChildren), 1, 1)
            inputs.Controls.Add(WrapField("Children 1-3 years old (free pax)", _availabilityFreeChildren), 0, 2)
            inputs.Controls.Add(WrapField("Search rooms (no., type, amenities)", _availabilitySearchText), 1, 2)

            Dim checkButton = MakeButton("Check Availability")
            AddHandler checkButton.Click, AddressOf CheckAvailabilityClicked
            inputs.SetColumnSpan(checkButton, 2)
            inputs.Controls.Add(checkButton, 0, 3)

            _availabilityLabel = New Label With {
                .Text = "Choose dates to see on-time room status.",
                .Dock = DockStyle.Top,
                .Height = 30,
                .ForeColor = _muted
            }
            panel.Controls.Add(_availabilityLabel)

            _roomsPanel = New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .Padding = New Padding(0, 8, 6, 0)
            }
            panel.Controls.Add(_roomsPanel)

            Return panel
        End Function

        Private Function BuildReservationPanel() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill

            Dim layout = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 12,
                .BackColor = _linen,
                .AutoScroll = True
            }
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 196))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 168))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 236))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 190))
            panel.Controls.Add(layout)

            layout.Controls.Add(MakeTitle("Room Booking"), 0, 0)

            _roomCombo = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
            AddHandler _roomCombo.SelectedIndexChanged, AddressOf TotalChanged

            _bookingCheckInPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _bookingCheckOutPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _bookingAdults = New NumericUpDown With {.Minimum = 1, .Maximum = 20, .Value = 2, .Dock = DockStyle.Fill}
            _bookingChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}
            _bookingFreeChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}
            AddHandler _bookingCheckInPicker.ValueChanged, AddressOf TotalChanged
            AddHandler _bookingCheckOutPicker.ValueChanged, AddressOf TotalChanged
            AddHandler _bookingAdults.ValueChanged, AddressOf BookingGuestsChanged
            AddHandler _bookingChildren.ValueChanged, AddressOf BookingGuestsChanged

            layout.Controls.Add(MakeSectionLabel("Booking details"), 0, 1)
            layout.Controls.Add(MakeTwoColumnGrid(
                WrapField("Room", _roomCombo),
                WrapField("Adults", _bookingAdults),
                WrapField("Children 4+ years old", _bookingChildren),
                WrapField("Children 1-3 years old (free pax)", _bookingFreeChildren),
                WrapField("Check-in", _bookingCheckInPicker),
                WrapField("Check-out", _bookingCheckOutPicker)), 0, 2)

            _guestNameText = New TextBox With {.Dock = DockStyle.Fill}
            _emailText = New TextBox With {.Dock = DockStyle.Fill}
            _phoneText = New TextBox With {.Dock = DockStyle.Fill}
            _addressText = New TextBox With {.Dock = DockStyle.Fill}

            layout.Controls.Add(MakeSectionLabel("Guest info"), 0, 3)
            layout.Controls.Add(MakeTwoColumnGrid(
                WrapField("Full name", _guestNameText),
                WrapField("Email", _emailText),
                WrapField("Phone", _phoneText),
                WrapField("Address", _addressText)), 0, 4)

            _addOnsPanel = New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False
            }
            layout.Controls.Add(MakeSectionLabel("Reserved add-ons and amenities"), 0, 5)
            layout.Controls.Add(_addOnsPanel, 0, 6)

            _paymentCombo = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
            _paymentCombo.Items.AddRange(New Object() {"Cash", "Card", "GCash", "Bank Transfer"})
            _paymentCombo.SelectedIndex = 0
            _paymentReferenceText = New TextBox With {.Dock = DockStyle.Fill}
            _notesText = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .Height = 76}

            Dim paymentLayout = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .BackColor = _linen
            }
            paymentLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            paymentLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 132))
            paymentLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            paymentLayout.Controls.Add(MakeSectionLabel("Payment"), 0, 0)
            paymentLayout.Controls.Add(MakeTwoColumnGrid(
                WrapField("Payment method", _paymentCombo),
                WrapField("Payment reference", _paymentReferenceText)), 0, 1)
            paymentLayout.Controls.Add(WrapField("Notes", _notesText, 104), 0, 2)

            _totalLabel = New Label With {
                .Text = "Total amount: PHP 0.00",
                .Dock = DockStyle.Fill,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold, GraphicsUnit.Point),
                .TextAlign = ContentAlignment.MiddleLeft
            }

            Dim actionPanel = New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(0, 6, 0, 0)
            }
            _reserveButton = MakeButton("Queue Reservation")
            AddHandler _reserveButton.Click, AddressOf ConfirmReservationClicked
            Dim printButton = MakeButton("Print Latest Receipt")
            AddHandler printButton.Click, AddressOf PrintLatestReceiptClicked
            actionPanel.Controls.Add(_reserveButton)
            actionPanel.Controls.Add(printButton)

            _receiptBox = New RichTextBox With {
                .Dock = DockStyle.Fill,
                .ReadOnly = True,
                .BorderStyle = BorderStyle.FixedSingle,
                .BackColor = _sand,
                .ForeColor = _espresso,
                .Text = "Latest booking receipt will appear here after you queue a reservation."
            }

            layout.Controls.Add(paymentLayout, 0, 7)
            layout.Controls.Add(_totalLabel, 0, 8)
            layout.Controls.Add(actionPanel, 0, 9)
            layout.Controls.Add(MakeSectionLabel("Latest booking receipt"), 0, 10)
            layout.Controls.Add(_receiptBox, 0, 11)

            Return panel
        End Function

        Private Function BuildHistoryPage() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill

            Dim layout = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .BackColor = _linen,
                .Padding = New Padding(0)
            }
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 52))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 48))

            Dim title = New Label With {
                .Text = "Reservation History",
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 19.0F, FontStyle.Bold, GraphicsUnit.Point)
            }
            layout.Controls.Add(title, 0, 0)

            Dim toolbar = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 4,
                .RowCount = 1,
                .Margin = New Padding(0),
                .Padding = New Padding(0, 6, 0, 6),
                .BackColor = _sand
            }
            toolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))

            _historyRefreshButton = MakeToolbarButton("Refresh")
            AddHandler _historyRefreshButton.Click, AddressOf RefreshHistoryClicked
            _viewHistoryButton = MakeToolbarButton("View Details")
            AddHandler _viewHistoryButton.Click, AddressOf ViewSelectedHistoryClicked
            _editHistoryButton = MakeToolbarButton("Edit Reservation")
            AddHandler _editHistoryButton.Click, AddressOf EditSelectedHistoryClicked
            _printHistoryButton = MakeToolbarButton("Print Receipt")
            AddHandler _printHistoryButton.Click, AddressOf PrintSelectedHistoryClicked
            toolbar.Controls.Add(_historyRefreshButton, 0, 0)
            toolbar.Controls.Add(_viewHistoryButton, 1, 0)
            toolbar.Controls.Add(_editHistoryButton, 2, 0)
            toolbar.Controls.Add(_printHistoryButton, 3, 0)
            layout.Controls.Add(toolbar, 0, 1)

            _historyList = New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .FullRowSelect = True,
                .GridLines = True,
                .HeaderStyle = ColumnHeaderStyle.Nonclickable,
                .BackColor = _linen,
                .ForeColor = _espresso,
                .Margin = New Padding(0, 6, 0, 6)
            }
            _historyList.Columns.Add("Code", 130)
            _historyList.Columns.Add("Guest", 180)
            _historyList.Columns.Add("Room", 150)
            _historyList.Columns.Add("Dates", 200)
            _historyList.Columns.Add("Guests", 160)
            _historyList.Columns.Add("Total", 110)
            _historyList.Columns.Add("Status", 110)
            AddHandler _historyList.SelectedIndexChanged, AddressOf HistorySelectionChanged
            layout.Controls.Add(_historyList, 0, 2)

            Dim detailsPanel = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Margin = New Padding(0, 6, 0, 0),
                .BackColor = _linen
            }
            detailsPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            detailsPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim detailsHeader = New Label With {
                .Text = "Reservation details",
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point)
            }
            detailsPanel.Controls.Add(detailsHeader, 0, 0)

            _historyDetailsBox = New RichTextBox With {
                .Dock = DockStyle.Fill,
                .ReadOnly = True,
                .BorderStyle = BorderStyle.FixedSingle,
                .BackColor = _sand,
                .ForeColor = _espresso,
                .Text = "Select a confirmed reservation to view details, print a receipt, or request changes."
            }
            detailsPanel.Controls.Add(_historyDetailsBox, 0, 1)
            layout.Controls.Add(detailsPanel, 0, 3)

            panel.Controls.Add(layout)
            Return panel
        End Function

        Private Function BuildNotificationPage() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill

            Dim layout = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .BackColor = _linen
            }
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim title = New Label With {
                .Text = "Email Notification and Guest Alerts",
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 19.0F, FontStyle.Bold, GraphicsUnit.Point)
            }
            layout.Controls.Add(title, 0, 0)

            Dim actionPanel = New Panel With {.Dock = DockStyle.Fill, .BackColor = _sand, .Padding = New Padding(0, 8, 0, 8)}
            _notificationRefreshButton = MakeToolbarButton("Refresh")
            _notificationRefreshButton.Width = 160
            _notificationRefreshButton.Dock = DockStyle.Left
            AddHandler _notificationRefreshButton.Click, AddressOf RefreshNotificationsClicked
            actionPanel.Controls.Add(_notificationRefreshButton)
            layout.Controls.Add(actionPanel, 0, 1)

            _notificationList = New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .FullRowSelect = True,
                .GridLines = True,
                .BackColor = _linen,
                .ForeColor = _espresso,
                .Margin = New Padding(0, 6, 0, 0)
            }
            _notificationList.Columns.Add("Code", 120)
            _notificationList.Columns.Add("Channel", 90)
            _notificationList.Columns.Add("Recipient", 190)
            _notificationList.Columns.Add("Subject", 210)
            _notificationList.Columns.Add("Message", 420)
            _notificationList.Columns.Add("Status", 120)
            layout.Controls.Add(_notificationList, 0, 2)

            panel.Controls.Add(layout)
            Return panel
        End Function

        Private Sub LoadInitialData()
            Dim tomorrow = DateTime.Today.AddDays(1)
            _checkInPicker.Value = tomorrow
            _checkOutPicker.Value = tomorrow.AddDays(1)
            _bookingCheckInPicker.Value = tomorrow
            _bookingCheckOutPicker.Value = tomorrow.AddDays(1)

            _addOns.Clear()
            _addOns.AddRange(_repository.GetAddOns())
            RenderAddOns()

            RefreshRooms()
            RefreshHistory()
            RefreshNotifications()
            PrefillGuestInfo()
        End Sub

        Private Sub PrefillGuestInfo()
            _guestNameText.Text = _account.FullName
            _emailText.Text = _account.Email
            _phoneText.Text = _account.Phone
        End Sub

        Private Sub CheckAvailabilityClicked(sender As Object, e As EventArgs)
            _bookingCheckInPicker.Value = _checkInPicker.Value.Date
            _bookingCheckOutPicker.Value = _checkOutPicker.Value.Date
            _bookingAdults.Value = _availabilityAdults.Value
            _bookingChildren.Value = _availabilityChildren.Value
            _bookingFreeChildren.Value = _availabilityFreeChildren.Value
            RefreshRooms()

            Dim chargeableGuests = CInt(_availabilityAdults.Value + _availabilityChildren.Value)
            Dim availableCount = _rooms.Where(Function(room) room.IsAvailable AndAlso room.Capacity >= chargeableGuests).Count()
            _availabilityLabel.Text = String.Format("{0} available room(s) found for {1} charged pax. Children ages 1-3 are free pax.", availableCount, chargeableGuests)
        End Sub

        Private Sub ConfirmReservationClicked(sender As Object, e As EventArgs)
            Try
                Dim selectedRoom = TryCast(_roomCombo.SelectedItem, RoomInfo)
                If selectedRoom Is Nothing Then
                    MessageBox.Show("Please select an available room.", "Room required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                Dim input = New ReservationInput With {
                    .RoomId = selectedRoom.Id,
                    .CheckIn = _bookingCheckInPicker.Value.Date,
                    .CheckOut = _bookingCheckOutPicker.Value.Date,
                    .AdultGuests = CInt(_bookingAdults.Value),
                    .ChildGuests = CInt(_bookingChildren.Value),
                    .FreeChildGuests = CInt(_bookingFreeChildren.Value),
                    .GuestName = _guestNameText.Text,
                    .Email = _emailText.Text,
                    .Phone = _phoneText.Text,
                    .Address = _addressText.Text,
                    .PaymentMethod = CStr(_paymentCombo.SelectedItem),
                    .PaymentReference = _paymentReferenceText.Text,
                    .Notes = _notesText.Text,
                    .AccountId = _account.Id,
                    .AddOns = CollectSelectedAddOns()
                }

                _latestReceipt = _repository.CreateReservation(input)
                RenderReceipt(_latestReceipt)
                ResetAddOnQuantities()
                RefreshRooms()
                RefreshHistory()
                RefreshNotifications()

                MessageBox.Show(
                    $"Reservation queued: {_latestReceipt.ConfirmationCode}{Environment.NewLine}Please wait for admin confirmation.",
                    "Reservation queued",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Reservation problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Sub EditSelectedHistoryClicked(sender As Object, e As EventArgs)
            If _historyList.SelectedItems.Count = 0 Then
                MessageBox.Show("Select a confirmed reservation from history to edit.", "No reservation selected", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim confirmationCode = _historyList.SelectedItems(0).Text
            Dim status = GetSelectedHistoryStatus()
            If Not String.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Only confirmed reservations can be edited. Pending reservations are waiting for admin confirmation. Change-pending reservations are waiting for admin approval.", "Cannot edit", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Try
                Using editForm As New ReservationEditForm(_repository, _account, confirmationCode)
                    If editForm.ShowDialog(Me) = DialogResult.OK Then
                        RefreshHistory()
                        RefreshNotifications()
                    End If
                End Using
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Edit problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Function GetSelectedHistoryStatus() As String
            If _historyList.SelectedItems.Count = 0 Then
                Return ""
            End If

            Return _historyList.SelectedItems(0).SubItems(6).Text
        End Function

        Private Sub UpdateHistoryActionState()
            Dim hasSelection = _historyList.SelectedItems.Count > 0
            Dim status = GetSelectedHistoryStatus()
            Dim isConfirmed = String.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase)
            Dim canPrint = isConfirmed OrElse String.Equals(status, "Change Pending", StringComparison.OrdinalIgnoreCase)

            _editHistoryButton.Enabled = hasSelection AndAlso isConfirmed
            _printHistoryButton.Enabled = hasSelection AndAlso canPrint
            _viewHistoryButton.Enabled = hasSelection
        End Sub

        Private Sub TabChanged(sender As Object, e As EventArgs)
            If _tabs.SelectedTab Is Nothing Then
                Return
            End If

            If _tabs.SelectedTab.Text = "Reserve History" Then
                RefreshHistory()
            ElseIf _tabs.SelectedTab.Text = "Email & Alerts" Then
                RefreshNotifications()
            End If
        End Sub

        Private Sub HistorySelectionChanged(sender As Object, e As EventArgs)
            UpdateHistoryActionState()
            If _historyList.SelectedItems.Count = 0 Then
                Return
            End If

            LoadHistoryDetails(_historyList.SelectedItems(0).Text)
        End Sub

        Private Sub ViewSelectedHistoryClicked(sender As Object, e As EventArgs)
            If _historyList.SelectedItems.Count = 0 Then
                MessageBox.Show("Select a reservation from history first.", "No reservation selected", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            LoadHistoryDetails(_historyList.SelectedItems(0).Text)
        End Sub

        Private Sub LoadHistoryDetails(confirmationCode As String)
            Try
                Dim receipt = _repository.GetReceiptForAccount(confirmationCode, _account.Id, _account.Email)
                If receipt Is Nothing Then
                    _historyDetailsBox.Text = "Could not load reservation details."
                    Return
                End If

                _historyDetailsBox.Text = FormatReceiptText(receipt)
            Catch ex As Exception
                _historyDetailsBox.Text = ex.Message
            End Try
        End Sub

        Private Sub PrintSelectedHistoryClicked(sender As Object, e As EventArgs)
            If _historyList.SelectedItems.Count = 0 Then
                MessageBox.Show("Select a reservation from history first.", "No reservation selected", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim status = GetSelectedHistoryStatus()
            If Not String.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) AndAlso
               Not String.Equals(status, "Change Pending", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Print receipt is available after the admin confirms the reservation.", "Not confirmed yet", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Try
                Dim confirmationCode = _historyList.SelectedItems(0).Text
                Dim receipt = _repository.GetReceiptForAccount(confirmationCode, _account.Id, _account.Email)
                If receipt Is Nothing Then
                    MessageBox.Show("Could not load the selected receipt.", "Receipt not found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                ShowReceiptDialog(receipt)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Print problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Sub RefreshHistoryClicked(sender As Object, e As EventArgs)
            RefreshHistory()
        End Sub

        Private Sub RefreshNotificationsClicked(sender As Object, e As EventArgs)
            RefreshNotifications()
        End Sub

        Private Sub PrintLatestReceiptClicked(sender As Object, e As EventArgs)
            If _latestReceipt Is Nothing Then
                MessageBox.Show("Create a reservation first to print a receipt.", "No receipt", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            If Not String.Equals(_latestReceipt.ReservationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) AndAlso
               Not String.Equals(_latestReceipt.ReservationStatus, "Change Pending", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Print receipt is available after admin confirms the reservation. You can also print from Reserve History once confirmed.", "Not confirmed yet", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ShowReceiptDialog(_latestReceipt)
        End Sub

        Private Sub ShowReceiptDialog(receipt As ReceiptInfo)
            Using receiptForm As New ReceiptForm(receipt)
                receiptForm.ShowDialog(Me)
            End Using
        End Sub

        Private Sub SelectRoomClicked(sender As Object, e As EventArgs)
            Dim button = TryCast(sender, Button)
            If button Is Nothing OrElse button.Tag Is Nothing Then
                Return
            End If

            Dim roomId = CInt(button.Tag)
            For index = 0 To _roomCombo.Items.Count - 1
                Dim room = TryCast(_roomCombo.Items(index), RoomInfo)
                If room IsNot Nothing AndAlso room.Id = roomId Then
                    _roomCombo.SelectedIndex = index
                    Exit For
                End If
            Next
        End Sub

        Private Sub TotalChanged(sender As Object, e As EventArgs)
            UpdateTotalPreview()
        End Sub

        Private Sub LogoutClicked(sender As Object, e As EventArgs)
            LogoutRequested = True
            Close()
        End Sub

        Private Sub BookingGuestsChanged(sender As Object, e As EventArgs)
            PopulateRoomCombo()
            UpdateTotalPreview()
        End Sub

        Private Sub RefreshRooms()
            _rooms.Clear()
            _rooms.AddRange(_repository.GetRooms(_bookingCheckInPicker.Value.Date, _bookingCheckOutPicker.Value.Date))
            RenderRooms()
            PopulateRoomCombo()
            UpdateTotalPreview()
        End Sub

        Private Sub AvailabilitySearchChanged(sender As Object, e As EventArgs)
            RenderRooms()
        End Sub

        Private Sub RenderRooms()
            _roomsPanel.Controls.Clear()
            Dim guestFilter = CInt(_availabilityAdults.Value + _availabilityChildren.Value)
            Dim search = If(_availabilitySearchText Is Nothing, "", _availabilitySearchText.Text.Trim())
            Dim displayRooms = _rooms.Where(Function(room) room.Capacity >= guestFilter).ToList()

            If Not String.IsNullOrWhiteSpace(search) Then
                Dim term = search.ToLowerInvariant()
                displayRooms = displayRooms.Where(Function(room)
                                                      Return room.RoomNumber.ToLowerInvariant().Contains(term) OrElse
                                                             room.RoomType.ToLowerInvariant().Contains(term) OrElse
                                                             room.Amenities.ToLowerInvariant().Contains(term) OrElse
                                                             room.Status.ToLowerInvariant().Contains(term)
                                                  End Function).ToList()
            End If

            If displayRooms.Count = 0 Then
                _roomsPanel.Controls.Add(New Label With {
                    .Text = "No room matches the selected guest count.",
                    .ForeColor = _muted,
                    .AutoSize = True
                })
                Return
            End If

            For Each room In displayRooms
                _roomsPanel.Controls.Add(MakeRoomCard(room))
            Next
        End Sub

        Private Function MakeRoomCard(room As RoomInfo) As Control
            Dim card = New Panel With {
                .Width = 410,
                .Height = 222,
                .BackColor = _linen,
                .Margin = New Padding(0, 0, 0, 12),
                .Padding = New Padding(14)
            }

            Dim status = New Label With {
                .Text = room.Status,
                .BackColor = If(room.IsAvailable, _green, _red),
                .ForeColor = Color.White,
                .AutoSize = False,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Width = 96,
                .Height = 28,
                .Location = New Point(14, 14)
            }
            card.Controls.Add(status)

            Dim price = New Label With {
                .Text = $"{room.Rate:C2}/night",
                .ForeColor = _coffee,
                .TextAlign = ContentAlignment.MiddleRight,
                .Location = New Point(214, 16),
                .Size = New Size(170, 25)
            }
            card.Controls.Add(price)

            Dim title = New Label With {
                .Text = $"Room {room.RoomNumber} - {room.RoomType}",
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 13.5F, FontStyle.Bold, GraphicsUnit.Point),
                .Location = New Point(14, 52),
                .Size = New Size(370, 28)
            }
            card.Controls.Add(title)

            Dim info = New Label With {
                .Text = $"Fits {room.Capacity} guest(s) - {room.Amenities}",
                .ForeColor = _muted,
                .Location = New Point(14, 82),
                .Size = New Size(370, 34)
            }
            card.Controls.Add(info)

            Dim calendar = New FlowLayoutPanel With {
                .Location = New Point(14, 118),
                .Size = New Size(370, 48),
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False
            }
            For Each dayInfo In _repository.GetRoomCalendar(room.Id, _checkInPicker.Value.Date, 7)
                Dim dayLabel = New Label With {
                    .Text = dayInfo.Date.ToString("MMM d"),
                    .BackColor = If(dayInfo.IsAvailable, _green, _red),
                    .ForeColor = Color.White,
                    .TextAlign = ContentAlignment.MiddleCenter,
                    .Size = New Size(50, 38),
                    .Margin = New Padding(0, 0, 3, 0)
                }
                calendar.Controls.Add(dayLabel)
            Next
            card.Controls.Add(calendar)

            Dim selectButton = MakeButton(If(room.IsAvailable, "Select room", "Not available"))
            selectButton.Location = New Point(14, 174)
            selectButton.Size = New Size(130, 32)
            selectButton.Tag = room.Id
            selectButton.Enabled = room.IsAvailable
            AddHandler selectButton.Click, AddressOf SelectRoomClicked
            card.Controls.Add(selectButton)

            Return card
        End Function

        Private Sub PopulateRoomCombo()
            Dim currentId As Integer? = Nothing
            Dim currentRoom = TryCast(_roomCombo.SelectedItem, RoomInfo)
            If currentRoom IsNot Nothing Then
                currentId = currentRoom.Id
            End If

            _roomCombo.Items.Clear()
            Dim availableRooms = _rooms.
                Where(Function(room) room.IsAvailable AndAlso room.Capacity >= CInt(_bookingAdults.Value + _bookingChildren.Value)).
                ToList()

            For Each room In availableRooms
                _roomCombo.Items.Add(room)
            Next

            If _roomCombo.Items.Count = 0 Then
                Return
            End If

            Dim selectedIndex = 0
            If currentId.HasValue Then
                For index = 0 To _roomCombo.Items.Count - 1
                    Dim room = CType(_roomCombo.Items(index), RoomInfo)
                    If room.Id = currentId.Value Then
                        selectedIndex = index
                        Exit For
                    End If
                Next
            End If

            _roomCombo.SelectedIndex = selectedIndex
        End Sub

        Private Sub RenderAddOns()
            _addOnsPanel.Controls.Clear()
            _addOnInputs.Clear()
            _addOnChecks.Clear()

            For Each addOn In _addOns
                Dim row = New TableLayoutPanel With {
                    .Width = 620,
                    .Height = 64,
                    .ColumnCount = 3,
                    .Margin = New Padding(0, 0, 0, 8),
                    .BackColor = _sand,
                    .Padding = New Padding(10)
                }
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 36))
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 72))
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 28))

                Dim selectedCheck = New CheckBox With {
                    .Dock = DockStyle.Fill,
                    .Text = "",
                    .TextAlign = ContentAlignment.MiddleCenter
                }

                Dim text = New Label With {
                    .Text = $"{addOn.Name} - {addOn.Description} ({addOn.Price:C2})",
                    .ForeColor = _espresso,
                    .Dock = DockStyle.Fill,
                    .TextAlign = ContentAlignment.MiddleLeft
                }
                Dim quantity = New NumericUpDown With {
                    .Minimum = 0,
                    .Maximum = 10,
                    .Value = 1,
                    .Enabled = False,
                    .Dock = DockStyle.Fill
                }
                AddHandler quantity.ValueChanged, AddressOf TotalChanged
                AddHandler selectedCheck.CheckedChanged,
                    Sub(sender, e)
                        quantity.Enabled = selectedCheck.Checked
                        If selectedCheck.Checked AndAlso quantity.Value = 0 Then
                            quantity.Value = 1
                        End If
                        UpdateTotalPreview()
                    End Sub

                row.Controls.Add(selectedCheck, 0, 0)
                row.Controls.Add(text, 1, 0)
                row.Controls.Add(quantity, 2, 0)
                _addOnsPanel.Controls.Add(row)
                _addOnChecks(addOn.Id) = selectedCheck
                _addOnInputs(addOn.Id) = quantity
            Next
        End Sub

        Private Function CollectSelectedAddOns() As List(Of SelectedAddOn)
            Dim selected As New List(Of SelectedAddOn)()
            For Each pair In _addOnInputs
                If _addOnChecks.ContainsKey(pair.Key) AndAlso _addOnChecks(pair.Key).Checked AndAlso pair.Value.Value > 0 Then
                    selected.Add(New SelectedAddOn With {.AddOnId = pair.Key, .Quantity = CInt(pair.Value.Value)})
                End If
            Next

            Return selected
        End Function

        Private Sub ResetAddOnQuantities()
            For Each quantityInput In _addOnInputs.Values
                quantityInput.Value = 1
                quantityInput.Enabled = False
            Next

            For Each selectedCheck In _addOnChecks.Values
                selectedCheck.Checked = False
            Next
        End Sub

        Private Sub UpdateTotalPreview()
            Dim room = TryCast(_roomCombo.SelectedItem, RoomInfo)
            Dim nights = Math.Max(1, CInt((_bookingCheckOutPicker.Value.Date - _bookingCheckInPicker.Value.Date).TotalDays))
            Dim roomSubtotal = If(room Is Nothing, 0D, room.Rate * nights)
            Dim addOnSubtotal = 0D

            For Each addOn In _addOns
                If _addOnInputs.ContainsKey(addOn.Id) AndAlso _addOnChecks.ContainsKey(addOn.Id) AndAlso _addOnChecks(addOn.Id).Checked Then
                    addOnSubtotal += addOn.Price * _addOnInputs(addOn.Id).Value
                End If
            Next

            _totalLabel.Text = $"Total amount: {(roomSubtotal + addOnSubtotal):C2}"
        End Sub

        Private Sub RenderReceipt(receipt As ReceiptInfo)
            _receiptBox.Text = FormatReceiptText(receipt)
        End Sub

        Private Shared Function FormatReceiptText(receipt As ReceiptInfo) As String
            Dim lines As New List(Of String) From {
                "HOTEL RESERVATION RECEIPT",
                $"Confirmation: {receipt.ConfirmationCode}",
                $"Status: {receipt.ReservationStatus}",
                $"Guest: {receipt.GuestName}",
                $"Email: {receipt.GuestEmail}",
                $"Phone: {receipt.GuestPhone}",
                $"Room: {receipt.RoomNumber} - {receipt.RoomType}",
                $"Amenities: {receipt.Amenities}",
                $"Stay: {receipt.CheckIn:MMM dd, yyyy} to {receipt.CheckOut:MMM dd, yyyy} ({receipt.Nights} night/s)",
                $"Guests: {receipt.AdultGuests} adult(s), {receipt.ChildGuests} child(ren) 4+, {receipt.FreeChildGuests} free child pax (1-3)",
                $"Payment: {receipt.PaymentMethod} - {receipt.PaymentStatus}",
                $"Reference: {receipt.PaymentReference}",
                "",
                $"Room subtotal: {receipt.RoomSubtotal:C2}",
                $"Add-ons: {receipt.AddOnSubtotal:C2}"
            }

            If receipt.AddOns.Count > 0 Then
                lines.Add("")
                lines.Add("Add-on items:")
                For Each addOn In receipt.AddOns
                    lines.Add($"- {addOn.Name} x {addOn.Quantity} = {addOn.Total:C2}")
                Next
            End If

            lines.Add("")
            lines.Add($"TOTAL PAID: {receipt.Total:C2}")
            Return String.Join(Environment.NewLine, lines)
        End Function

        Private Sub RefreshHistory()
            _historyList.Items.Clear()
            For Each item In _repository.GetReservationHistory(_account.Id, _account.Email)
                Dim row = New ListViewItem(item.ConfirmationCode)
                row.SubItems.Add(item.GuestName)
                row.SubItems.Add($"{item.RoomNumber} {item.RoomType}")
                row.SubItems.Add($"{item.CheckIn:MMM dd, yyyy} - {item.CheckOut:MMM dd, yyyy}")
                row.SubItems.Add($"{item.AdultGuests} adult, {item.ChildGuests} child 4+, {item.FreeChildGuests} free")
                row.SubItems.Add($"{item.Total:C2}")
                row.SubItems.Add(item.Status)
                _historyList.Items.Add(row)
            Next

            If _historyList.Items.Count > 0 AndAlso _historyList.SelectedItems.Count = 0 Then
                _historyList.Items(0).Selected = True
            ElseIf _historyList.SelectedItems.Count > 0 Then
                LoadHistoryDetails(_historyList.SelectedItems(0).Text)
            Else
                _historyDetailsBox.Text = "No reservations yet. Queue a booking and it will appear here after admin confirmation."
            End If

            UpdateHistoryActionState()
        End Sub

        Private Sub RefreshNotifications()
            _notificationList.Items.Clear()
            For Each item In _repository.GetNotifications(_account.Email, _account.Phone)
                Dim row = New ListViewItem(item.ConfirmationCode)
                row.SubItems.Add(item.Channel)
                row.SubItems.Add(item.Recipient)
                row.SubItems.Add(item.Subject)
                row.SubItems.Add(item.Message)
                row.SubItems.Add(item.Status)
                _notificationList.Items.Add(row)
            Next
        End Sub

        Private Function MakeCardPanel() As Panel
            Return New Panel With {
                .BackColor = _linen,
                .Padding = New Padding(18),
                .BorderStyle = BorderStyle.FixedSingle
            }
        End Function

        Private Function MakeTitle(text As String) As Label
            Return New Label With {
                .Text = text,
                .Dock = DockStyle.Top,
                .Height = 42,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 19.0F, FontStyle.Bold, GraphicsUnit.Point)
            }
        End Function

        Private Function MakeSectionLabel(text As String) As Label
            Return New Label With {
                .Text = text,
                .Dock = DockStyle.Top,
                .Height = 34,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
                .Padding = New Padding(0, 10, 0, 0)
            }
        End Function

        Private Function MakeButton(text As String) As Button
            Return New Button With {
                .Text = text,
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Height = 40,
                .Width = 170,
                .Margin = New Padding(0, 6, 8, 6)
            }
        End Function

        Private Function MakeToolbarButton(text As String) As Button
            Dim button = New Button With {
                .Text = text,
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(6, 4, 6, 4),
                .TabStop = True,
                .Cursor = Cursors.Hand,
                .Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point)
            }
            button.FlatAppearance.BorderSize = 0
            Return button
        End Function

        Private Function WrapField(labelText As String, control As Control, Optional height As Integer = 58) As Control
            Dim panel = New Panel With {.Dock = DockStyle.Fill, .Height = height, .Padding = New Padding(0, 0, 8, 8)}
            Dim label = New Label With {
                .Text = labelText,
                .Dock = DockStyle.Top,
                .Height = 20,
                .ForeColor = _muted,
                .Font = New Font("Segoe UI", 8.8F, FontStyle.Bold, GraphicsUnit.Point)
            }
            control.Dock = DockStyle.Fill
            panel.Controls.Add(control)
            panel.Controls.Add(label)
            Return panel
        End Function

        Private Function MakeTwoColumnGrid(ParamArray controls As Control()) As Control
            Dim rows = CInt(Math.Ceiling(controls.Length / 2.0))
            Dim grid = New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .ColumnCount = 2,
                .RowCount = rows,
                .Height = rows * 66,
                .Padding = New Padding(0, 0, 0, 4)
            }
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))

            For i = 0 To rows - 1
                grid.RowStyles.Add(New RowStyle(SizeType.Absolute, 66))
            Next

            For index = 0 To controls.Length - 1
                grid.Controls.Add(controls(index), index Mod 2, index \ 2)
            Next

            Return grid
        End Function
    End Class
End Namespace

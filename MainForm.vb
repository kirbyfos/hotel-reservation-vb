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
                .Height = 218,
                .Padding = New Padding(0, 8, 0, 0)
            }
            inputs.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            inputs.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            inputs.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            panel.Controls.Add(inputs)

            _checkInPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _checkOutPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _availabilityAdults = New NumericUpDown With {.Minimum = 1, .Maximum = 20, .Value = 2, .Dock = DockStyle.Fill}
            _availabilityChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}
            _availabilityFreeChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Value = 0, .Dock = DockStyle.Fill}

            inputs.Controls.Add(WrapField("Check-in", _checkInPicker), 0, 0)
            inputs.Controls.Add(WrapField("Check-out", _checkOutPicker), 1, 0)
            inputs.Controls.Add(WrapField("Adults", _availabilityAdults), 0, 1)
            inputs.Controls.Add(WrapField("Children 4+ years old", _availabilityChildren), 1, 1)
            inputs.Controls.Add(WrapField("Children 1-3 years old (free pax)", _availabilityFreeChildren), 0, 2)

            Dim checkButton = MakeButton("Check Availability")
            AddHandler checkButton.Click, AddressOf CheckAvailabilityClicked
            inputs.Controls.Add(checkButton, 1, 2)

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
            panel.AutoScroll = True

            panel.Controls.Add(MakeTitle("Room Booking"))

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

            panel.Controls.Add(MakeSectionLabel("Booking details"))
            panel.Controls.Add(MakeTwoColumnGrid(
                WrapField("Room", _roomCombo),
                WrapField("Adults", _bookingAdults),
                WrapField("Children 4+ years old", _bookingChildren),
                WrapField("Children 1-3 years old (free pax)", _bookingFreeChildren),
                WrapField("Check-in", _bookingCheckInPicker),
                WrapField("Check-out", _bookingCheckOutPicker)))

            _guestNameText = New TextBox With {.Dock = DockStyle.Fill}
            _emailText = New TextBox With {.Dock = DockStyle.Fill}
            _phoneText = New TextBox With {.Dock = DockStyle.Fill}
            _addressText = New TextBox With {.Dock = DockStyle.Fill}

            panel.Controls.Add(MakeSectionLabel("Guest info"))
            panel.Controls.Add(MakeTwoColumnGrid(
                WrapField("Full name", _guestNameText),
                WrapField("Email", _emailText),
                WrapField("Phone", _phoneText),
                WrapField("Address", _addressText)))

            _addOnsPanel = New FlowLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False
            }
            panel.Controls.Add(MakeSectionLabel("Reserved add-ons and amenities"))
            panel.Controls.Add(_addOnsPanel)

            _paymentCombo = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
            _paymentCombo.Items.AddRange(New Object() {"Cash", "Card", "GCash", "Bank Transfer"})
            _paymentCombo.SelectedIndex = 0
            _paymentReferenceText = New TextBox With {.Dock = DockStyle.Fill}
            _notesText = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .Height = 76}

            panel.Controls.Add(MakeSectionLabel("Payment"))
            panel.Controls.Add(MakeTwoColumnGrid(
                WrapField("Payment method", _paymentCombo),
                WrapField("Payment reference", _paymentReferenceText)))
            panel.Controls.Add(WrapField("Notes", _notesText, 104))

            _totalLabel = New Label With {
                .Text = "Total: PHP 0.00",
                .Dock = DockStyle.Top,
                .Height = 38,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold, GraphicsUnit.Point),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            panel.Controls.Add(_totalLabel)

            Dim actionPanel = New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 54, .FlowDirection = FlowDirection.LeftToRight}
            Dim reserveButton = MakeButton("Queue Reservation")
            AddHandler reserveButton.Click, AddressOf ConfirmReservationClicked
            Dim printButton = MakeButton("Print Latest Receipt")
            AddHandler printButton.Click, AddressOf PrintLatestReceiptClicked
            actionPanel.Controls.Add(reserveButton)
            actionPanel.Controls.Add(printButton)
            panel.Controls.Add(actionPanel)

            _receiptBox = New RichTextBox With {
                .Dock = DockStyle.Top,
                .Height = 170,
                .ReadOnly = True,
                .BorderStyle = BorderStyle.None,
                .BackColor = _sand,
                .ForeColor = _espresso,
                .Text = "Latest booking receipt will appear here."
            }
            panel.Controls.Add(MakeSectionLabel("Receipt"))
            panel.Controls.Add(_receiptBox)

            Return panel
        End Function

        Private Function BuildHistoryPage() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill
            panel.Controls.Add(MakeTitle("Reservation History"))

            _historyList = New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .FullRowSelect = True,
                .GridLines = False,
                .BackColor = _linen,
                .ForeColor = _espresso
            }
            _historyList.Columns.Add("Code", 130)
            _historyList.Columns.Add("Guest", 200)
            _historyList.Columns.Add("Room", 170)
            _historyList.Columns.Add("Dates", 210)
            _historyList.Columns.Add("Guests", 180)
            _historyList.Columns.Add("Total", 130)
            _historyList.Columns.Add("Status", 120)
            panel.Controls.Add(_historyList)

            Return panel
        End Function

        Private Function BuildNotificationPage() As Control
            Dim panel = MakeCardPanel()
            panel.Dock = DockStyle.Fill
            panel.Controls.Add(MakeTitle("Email Notification and Guest Alerts"))

            _notificationList = New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .FullRowSelect = True,
                .BackColor = _linen,
                .ForeColor = _espresso
            }
            _notificationList.Columns.Add("Code", 120)
            _notificationList.Columns.Add("Channel", 90)
            _notificationList.Columns.Add("Recipient", 190)
            _notificationList.Columns.Add("Subject", 210)
            _notificationList.Columns.Add("Message", 420)
            _notificationList.Columns.Add("Status", 120)
            panel.Controls.Add(_notificationList)

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

        Private Sub PrintLatestReceiptClicked(sender As Object, e As EventArgs)
            If _latestReceipt Is Nothing Then
                MessageBox.Show("Create a reservation first to print a receipt.", "No receipt", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using receiptForm As New ReceiptForm(_latestReceipt)
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

        Private Sub RenderRooms()
            _roomsPanel.Controls.Clear()
            Dim guestFilter = CInt(_availabilityAdults.Value + _availabilityChildren.Value)
            Dim displayRooms = _rooms.Where(Function(room) room.Capacity >= guestFilter).ToList()

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

            _totalLabel.Text = $"Total: {(roomSubtotal + addOnSubtotal):C2}"
        End Sub

        Private Sub RenderReceipt(receipt As ReceiptInfo)
            Dim lines As New List(Of String) From {
                "HOTEL RESERVATION RECEIPT",
                $"Confirmation: {receipt.ConfirmationCode}",
                $"Guest: {receipt.GuestName}",
                $"Room: {receipt.RoomNumber} - {receipt.RoomType}",
                $"Stay: {receipt.CheckIn:MMM dd, yyyy} to {receipt.CheckOut:MMM dd, yyyy} ({receipt.Nights} night/s)",
                $"Guests: {receipt.AdultGuests} adult(s), {receipt.ChildGuests} child(ren) 4+, {receipt.FreeChildGuests} free child pax (1-3)",
                $"Payment: {receipt.PaymentMethod} - {receipt.PaymentStatus}",
                $"Reference: {receipt.PaymentReference}",
                "",
                $"Room subtotal: {receipt.RoomSubtotal:C2}",
                $"Add-ons: {receipt.AddOnSubtotal:C2}",
                $"TOTAL PAID: {receipt.Total:C2}"
            }

            _receiptBox.Text = String.Join(Environment.NewLine, lines)
        End Sub

        Private Sub RefreshHistory()
            _historyList.Items.Clear()
            For Each item In _repository.GetReservationHistory()
                Dim row = New ListViewItem(item.ConfirmationCode)
                row.SubItems.Add(item.GuestName)
                row.SubItems.Add($"{item.RoomNumber} {item.RoomType}")
                row.SubItems.Add($"{item.CheckIn:MMM dd, yyyy} - {item.CheckOut:MMM dd, yyyy}")
                row.SubItems.Add($"{item.AdultGuests} adult, {item.ChildGuests} child 4+, {item.FreeChildGuests} free")
                row.SubItems.Add($"{item.Total:C2}")
                row.SubItems.Add(item.Status)
                _historyList.Items.Add(row)
            Next
        End Sub

        Private Sub RefreshNotifications()
            _notificationList.Items.Clear()
            For Each item In _repository.GetNotifications()
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

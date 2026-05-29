Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace HotelReservation
    Public Class ReservationEditForm
        Inherits Form

        Private ReadOnly _repository As HotelRepository
        Private ReadOnly _account As AccountInfo
        Private ReadOnly _confirmationCode As String
        Private ReadOnly _detail As ReservationDetailInfo
        Private ReadOnly _addOns As List(Of AddOnInfo)
        Private ReadOnly _addOnInputs As New Dictionary(Of Integer, NumericUpDown)()
        Private ReadOnly _addOnChecks As New Dictionary(Of Integer, CheckBox)()
        Private ReadOnly _rooms As New List(Of RoomInfo)()

        Private ReadOnly _cream As Color = Color.FromArgb(247, 239, 227)
        Private ReadOnly _linen As Color = Color.FromArgb(255, 250, 241)
        Private ReadOnly _coffee As Color = Color.FromArgb(106, 73, 52)
        Private ReadOnly _espresso As Color = Color.FromArgb(53, 35, 24)
        Private ReadOnly _muted As Color = Color.FromArgb(125, 102, 83)
        Private ReadOnly _sand As Color = Color.FromArgb(234, 216, 191)

        Private _roomCombo As ComboBox
        Private _checkInPicker As DateTimePicker
        Private _checkOutPicker As DateTimePicker
        Private _adults As NumericUpDown
        Private _children As NumericUpDown
        Private _freeChildren As NumericUpDown
        Private _guestNameText As TextBox
        Private _emailText As TextBox
        Private _phoneText As TextBox
        Private _addressText As TextBox
        Private _paymentCombo As ComboBox
        Private _paymentReferenceText As TextBox
        Private _notesText As TextBox
        Private _addOnsPanel As FlowLayoutPanel
        Private _totalLabel As Label

        Public Sub New(repository As HotelRepository, account As AccountInfo, confirmationCode As String)
            _repository = repository
            _account = account
            _confirmationCode = confirmationCode

            Dim detail = repository.GetReservationForEdit(confirmationCode, account.Id, account.Email)
            If detail Is Nothing Then
                Throw New InvalidOperationException("Could not load the selected reservation.")
            End If

            If Not String.Equals(detail.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("Only confirmed reservations can be edited.")
            End If

            _detail = detail
            _addOns = repository.GetAddOns()

            Text = $"Edit Reservation - {confirmationCode}"
            StartPosition = FormStartPosition.CenterParent
            MinimumSize = New Size(760, 680)
            Size = New Size(820, 760)
            BackColor = _cream
            Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)

            BuildLayout()
            LoadDetail()
        End Sub

        Private Sub BuildLayout()
            Dim root = New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(18), .BackColor = _linen}

            Dim title = New Label With {
                .Text = $"Update reservation {_confirmationCode}",
                .Dock = DockStyle.Top,
                .Height = 42,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 18.0F, FontStyle.Bold, GraphicsUnit.Point)
            }

            Dim hint = New Label With {
                .Text = "Changes are sent to the admin for approval. You will be notified once approved.",
                .Dock = DockStyle.Top,
                .Height = 28,
                .ForeColor = _muted
            }

            Dim scroll = New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = _linen}

            _roomCombo = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
            _checkInPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _checkOutPicker = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Dock = DockStyle.Fill}
            _adults = New NumericUpDown With {.Minimum = 1, .Maximum = 20, .Dock = DockStyle.Fill}
            _children = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Dock = DockStyle.Fill}
            _freeChildren = New NumericUpDown With {.Minimum = 0, .Maximum = 20, .Dock = DockStyle.Fill}
            AddHandler _checkInPicker.ValueChanged, AddressOf RecalculateTotal
            AddHandler _checkOutPicker.ValueChanged, AddressOf RecalculateTotal
            AddHandler _roomCombo.SelectedIndexChanged, AddressOf RecalculateTotal
            AddHandler _adults.ValueChanged, AddressOf GuestsChanged
            AddHandler _children.ValueChanged, AddressOf GuestsChanged

            _guestNameText = New TextBox With {.Dock = DockStyle.Fill}
            _emailText = New TextBox With {.Dock = DockStyle.Fill}
            _phoneText = New TextBox With {.Dock = DockStyle.Fill}
            _addressText = New TextBox With {.Dock = DockStyle.Fill}

            _paymentCombo = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
            _paymentCombo.Items.AddRange(New Object() {"Cash", "Card", "GCash", "Bank Transfer"})
            _paymentReferenceText = New TextBox With {.Dock = DockStyle.Fill}
            _notesText = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .Height = 72}

            _addOnsPanel = New FlowLayoutPanel With {
                .AutoSize = True,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .Width = 720
            }
            RenderAddOns()

            _totalLabel = New Label With {
                .Text = "Total: PHP 0.00",
                .AutoSize = False,
                .Height = 34,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold, GraphicsUnit.Point)
            }

            Dim content = New FlowLayoutPanel With {
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False,
                .AutoSize = True,
                .Width = 740,
                .Padding = New Padding(0, 8, 0, 0)
            }
            content.Controls.Add(MakeSection("Booking details"))
            content.Controls.Add(MakeGrid(
                WrapField("Room", _roomCombo),
                WrapField("Adults", _adults),
                WrapField("Children 4+ years old", _children),
                WrapField("Children 1-3 years old (free pax)", _freeChildren),
                WrapField("Check-in", _checkInPicker),
                WrapField("Check-out", _checkOutPicker)))
            content.Controls.Add(MakeSection("Guest info"))
            content.Controls.Add(MakeGrid(
                WrapField("Full name", _guestNameText),
                WrapField("Email", _emailText),
                WrapField("Phone", _phoneText),
                WrapField("Address", _addressText)))
            content.Controls.Add(MakeSection("Add-ons"))
            content.Controls.Add(_addOnsPanel)
            content.Controls.Add(MakeSection("Payment"))
            content.Controls.Add(MakeGrid(
                WrapField("Payment method", _paymentCombo),
                WrapField("Payment reference", _paymentReferenceText)))
            content.Controls.Add(WrapField("Notes", _notesText, 96))
            content.Controls.Add(_totalLabel)

            scroll.Controls.Add(content)

            Dim actions = New FlowLayoutPanel With {
                .Dock = DockStyle.Bottom,
                .Height = 52,
                .FlowDirection = FlowDirection.RightToLeft,
                .Padding = New Padding(0, 8, 0, 0)
            }
            Dim saveButton = MakeButton("Save Changes")
            AddHandler saveButton.Click, AddressOf SaveClicked
            Dim cancelButton = MakeButton("Cancel")
            AddHandler cancelButton.Click, Sub(sender, e) DialogResult = DialogResult.Cancel
            actions.Controls.Add(saveButton)
            actions.Controls.Add(cancelButton)

            root.Controls.Add(actions)
            root.Controls.Add(scroll)
            root.Controls.Add(hint)
            root.Controls.Add(title)
            Controls.Add(root)
        End Sub

        Private Sub LoadDetail()
            _checkInPicker.Value = _detail.CheckIn
            _checkOutPicker.Value = _detail.CheckOut
            _adults.Value = _detail.AdultGuests
            _children.Value = _detail.ChildGuests
            _freeChildren.Value = _detail.FreeChildGuests
            _guestNameText.Text = _detail.GuestName
            _emailText.Text = _detail.Email
            _phoneText.Text = _detail.Phone
            _addressText.Text = _detail.Address
            _notesText.Text = _detail.Notes

            Dim paymentIndex = _paymentCombo.Items.IndexOf(_detail.PaymentMethod)
            If paymentIndex >= 0 Then
                _paymentCombo.SelectedIndex = paymentIndex
            Else
                _paymentCombo.SelectedIndex = 0
            End If
            _paymentReferenceText.Text = _detail.PaymentReference

            For Each addOn In _detail.AddOns
                If _addOnChecks.ContainsKey(addOn.AddOnId) Then
                    _addOnChecks(addOn.AddOnId).Checked = True
                    _addOnInputs(addOn.AddOnId).Enabled = True
                    _addOnInputs(addOn.AddOnId).Value = Math.Max(1, addOn.Quantity)
                End If
            Next

            RefreshRooms()
            SelectRoom(_detail.RoomId)
            RecalculateTotal(Nothing, EventArgs.Empty)
        End Sub

        Private Sub GuestsChanged(sender As Object, e As EventArgs)
            RefreshRooms()
            RecalculateTotal(Nothing, EventArgs.Empty)
        End Sub

        Private Sub RefreshRooms()
            Dim reservationId = _repository.GetReservationId(_confirmationCode, _account.Id, _account.Email)
            _rooms.Clear()
            _rooms.AddRange(_repository.GetRooms(_checkInPicker.Value.Date, _checkOutPicker.Value.Date, reservationId))

            Dim currentRoomId = _detail.RoomId
            Dim selectedRoom = TryCast(_roomCombo.SelectedItem, RoomInfo)
            If selectedRoom IsNot Nothing Then
                currentRoomId = selectedRoom.Id
            End If

            _roomCombo.Items.Clear()
            Dim chargeableGuests = CInt(_adults.Value + _children.Value)
            Dim availableRooms = _rooms.Where(Function(room) room.IsAvailable AndAlso room.Capacity >= chargeableGuests).ToList()

            For Each room In availableRooms
                _roomCombo.Items.Add(room)
            Next

            Dim forcedRoom = _rooms.FirstOrDefault(Function(room) room.Id = currentRoomId)
            If forcedRoom IsNot Nothing AndAlso availableRooms.All(Function(room) room.Id <> currentRoomId) Then
                _roomCombo.Items.Add(forcedRoom)
            End If

            If _roomCombo.Items.Count > 0 Then
                SelectRoom(currentRoomId)
            End If
        End Sub

        Private Sub SelectRoom(roomId As Integer)
            For index = 0 To _roomCombo.Items.Count - 1
                Dim room = TryCast(_roomCombo.Items(index), RoomInfo)
                If room IsNot Nothing AndAlso room.Id = roomId Then
                    _roomCombo.SelectedIndex = index
                    Return
                End If
            Next

            If _roomCombo.Items.Count > 0 Then
                _roomCombo.SelectedIndex = 0
            End If
        End Sub

        Private Sub RenderAddOns()
            _addOnsPanel.Controls.Clear()
            _addOnInputs.Clear()
            _addOnChecks.Clear()

            For Each addOn In _addOns
                Dim row = New TableLayoutPanel With {
                    .Width = 700,
                    .Height = 58,
                    .ColumnCount = 3,
                    .Margin = New Padding(0, 0, 0, 8),
                    .BackColor = _sand,
                    .Padding = New Padding(10)
                }
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 36))
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 72))
                row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 28))

                Dim selectedCheck = New CheckBox With {.Dock = DockStyle.Fill}
                Dim text = New Label With {
                    .Text = $"{addOn.Name} - {addOn.Description} ({addOn.Price:C2})",
                    .ForeColor = _espresso,
                    .Dock = DockStyle.Fill,
                    .TextAlign = ContentAlignment.MiddleLeft
                }
                Dim quantity = New NumericUpDown With {.Minimum = 0, .Maximum = 10, .Value = 1, .Enabled = False, .Dock = DockStyle.Fill}
                AddHandler quantity.ValueChanged, AddressOf RecalculateTotal
                AddHandler selectedCheck.CheckedChanged,
                    Sub(sender, e)
                        quantity.Enabled = selectedCheck.Checked
                        If selectedCheck.Checked AndAlso quantity.Value = 0 Then
                            quantity.Value = 1
                        End If
                        RecalculateTotal(Nothing, EventArgs.Empty)
                    End Sub

                row.Controls.Add(selectedCheck, 0, 0)
                row.Controls.Add(text, 1, 0)
                row.Controls.Add(quantity, 2, 0)
                _addOnsPanel.Controls.Add(row)
                _addOnChecks(addOn.Id) = selectedCheck
                _addOnInputs(addOn.Id) = quantity
            Next
        End Sub

        Private Sub RecalculateTotal(sender As Object, e As EventArgs)
            Dim room = TryCast(_roomCombo.SelectedItem, RoomInfo)
            Dim nights = Math.Max(1, CInt((_checkOutPicker.Value.Date - _checkInPicker.Value.Date).TotalDays))
            Dim roomSubtotal = If(room Is Nothing, 0D, room.Rate * nights)
            Dim addOnSubtotal = 0D

            For Each addOn In _addOns
                If _addOnInputs.ContainsKey(addOn.Id) AndAlso _addOnChecks.ContainsKey(addOn.Id) AndAlso _addOnChecks(addOn.Id).Checked Then
                    addOnSubtotal += addOn.Price * _addOnInputs(addOn.Id).Value
                End If
            Next

            _totalLabel.Text = $"Total: {(roomSubtotal + addOnSubtotal):C2}"
        End Sub

        Private Sub SaveClicked(sender As Object, e As EventArgs)
            Try
                Dim selectedRoom = TryCast(_roomCombo.SelectedItem, RoomInfo)
                If selectedRoom Is Nothing Then
                    MessageBox.Show("Please select a room.", "Room required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                Dim input = New ReservationInput With {
                    .RoomId = selectedRoom.Id,
                    .CheckIn = _checkInPicker.Value.Date,
                    .CheckOut = _checkOutPicker.Value.Date,
                    .AdultGuests = CInt(_adults.Value),
                    .ChildGuests = CInt(_children.Value),
                    .FreeChildGuests = CInt(_freeChildren.Value),
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

                _repository.UpdateReservation(_confirmationCode, _account.Id, _account.Email, input)
                MessageBox.Show(
                    $"Changes submitted for {_confirmationCode}.{Environment.NewLine}Please wait for admin approval.",
                    "Changes submitted",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                DialogResult = DialogResult.OK
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Update problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
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

        Private Function MakeSection(text As String) As Label
            Return New Label With {
                .Text = text,
                .AutoSize = False,
                .Width = 720,
                .Height = 30,
                .ForeColor = _coffee,
                .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
                .Padding = New Padding(0, 8, 0, 0)
            }
        End Function

        Private Function MakeButton(text As String) As Button
            Return New Button With {
                .Text = text,
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Width = 150,
                .Height = 38,
                .Margin = New Padding(8, 0, 0, 0)
            }
        End Function

        Private Function WrapField(labelText As String, control As Control, Optional height As Integer = 58) As Control
            Dim panel = New Panel With {.Width = 350, .Height = height, .Padding = New Padding(0, 0, 8, 8)}
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

        Private Function MakeGrid(ParamArray controls As Control()) As Control
            Dim rows = CInt(Math.Ceiling(controls.Length / 2.0))
            Dim grid = New TableLayoutPanel With {
                .ColumnCount = 2,
                .RowCount = rows,
                .AutoSize = True,
                .Width = 720
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

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Windows.Forms

Namespace HotelReservation
    Public Class ReceiptForm
        Inherits Form

        Private ReadOnly _receipt As ReceiptInfo
        Private ReadOnly _receiptText As String
        Private ReadOnly _printDocument As New PrintDocument()
        Private ReadOnly _coffee As Color = Color.FromArgb(106, 73, 52)
        Private ReadOnly _cream As Color = Color.FromArgb(247, 239, 227)
        Private ReadOnly _linen As Color = Color.FromArgb(255, 250, 241)
        Private ReadOnly _espresso As Color = Color.FromArgb(53, 35, 24)

        Public Sub New(receipt As ReceiptInfo)
            _receipt = receipt
            _receiptText = BuildReceiptText(receipt)

            Text = $"Printable Receipt - {receipt.ConfirmationCode}"
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(720, 760)
            BackColor = _cream
            Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)

            AddHandler _printDocument.PrintPage, AddressOf PrintPage
            BuildLayout()
        End Sub

        Private Sub BuildLayout()
            Dim root = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(18)
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(root)

            Dim actionPanel = New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .Padding = New Padding(0, 0, 0, 8)
            }
            Dim closeButton = New Button With {
                .Text = "Close",
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Width = 120,
                .Height = 40
            }
            AddHandler closeButton.Click, Sub(sender, e) Close()
            Dim printButton = New Button With {
                .Text = "Print Receipt",
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Width = 150,
                .Height = 40,
                .Margin = New Padding(0, 0, 10, 0)
            }
            AddHandler printButton.Click, AddressOf PrintClicked
            actionPanel.Controls.Add(closeButton)
            actionPanel.Controls.Add(printButton)
            root.Controls.Add(actionPanel, 0, 0)

            Dim receiptBox = New RichTextBox With {
                .Dock = DockStyle.Fill,
                .ReadOnly = True,
                .BackColor = _linen,
                .ForeColor = _espresso,
                .BorderStyle = BorderStyle.FixedSingle,
                .Font = New Font("Consolas", 10.5F, FontStyle.Regular, GraphicsUnit.Point),
                .Text = _receiptText
            }
            root.Controls.Add(receiptBox, 0, 1)
        End Sub

        Private Sub PrintClicked(sender As Object, e As EventArgs)
            Try
                Using dialog As New PrintDialog()
                    dialog.Document = _printDocument
                    dialog.UseEXDialog = True
                    If dialog.ShowDialog(Me) = DialogResult.OK Then
                        _printDocument.Print()
                        MessageBox.Show("Receipt sent to the printer.", "Print complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End Using
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Print problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Sub PrintPage(sender As Object, e As PrintPageEventArgs)
            If e.Graphics Is Nothing Then
                Return
            End If

            Using font As New Font("Consolas", 10.5F)
                e.Graphics.DrawString(_receiptText, font, Brushes.Black, e.MarginBounds)
            End Using
        End Sub

        Private Shared Function BuildReceiptText(receipt As ReceiptInfo) As String
            Dim lines As New List(Of String) From {
                "CASA RESERVE HOTEL",
                "Printable Reservation Receipt",
                New String("-"c, 48),
                $"Confirmation Code : {receipt.ConfirmationCode}",
                $"Created At        : {receipt.CreatedAt:MMM dd, yyyy hh:mm tt}",
                $"Reservation       : {receipt.ReservationStatus}",
                "",
                "GUEST INFORMATION",
                $"Name              : {receipt.GuestName}",
                $"Email             : {receipt.GuestEmail}",
                $"Phone             : {receipt.GuestPhone}",
                "",
                "ROOM BOOKING",
                $"Room              : {receipt.RoomNumber} - {receipt.RoomType}",
                $"Amenities         : {receipt.Amenities}",
                $"Check-in          : {receipt.CheckIn:MMM dd, yyyy}",
                $"Check-out         : {receipt.CheckOut:MMM dd, yyyy}",
                $"Nights            : {receipt.Nights}",
                $"Adults            : {receipt.AdultGuests}",
                $"Children 4+       : {receipt.ChildGuests}",
                $"Free child pax    : {receipt.FreeChildGuests} (ages 1-3)",
                "",
                "ADD-ONS"
            }

            If receipt.AddOns.Count = 0 Then
                lines.Add("No add-ons selected.")
            Else
                For Each addOn In receipt.AddOns
                    lines.Add($"{addOn.Name} x {addOn.Quantity} @ {addOn.UnitPrice:C2} = {addOn.Total:C2}")
                Next
            End If

            lines.AddRange(New String() {
                "",
                "PAYMENT",
                $"Method            : {receipt.PaymentMethod}",
                $"Status            : {receipt.PaymentStatus}",
                $"Reference         : {receipt.PaymentReference}",
                "",
                "SUMMARY",
                $"Room subtotal     : {receipt.RoomSubtotal:C2}",
                $"Add-ons           : {receipt.AddOnSubtotal:C2}",
                New String("-"c, 48),
                $"TOTAL PAID        : {receipt.Total:C2}",
                New String("-"c, 48),
                "Thank you for choosing Casa Reserve."
            })

            Return String.Join(Environment.NewLine, lines)
        End Function
    End Class
End Namespace

Imports System
Imports System.Collections.Generic

Namespace HotelReservation
    Public Class RoomInfo
        Public Property Id As Integer
        Public Property RoomNumber As String = ""
        Public Property RoomType As String = ""
        Public Property Capacity As Integer
        Public Property Rate As Decimal
        Public Property Amenities As String = ""
        Public Property Status As String = ""
        Public Property IsAvailable As Boolean

        Public Overrides Function ToString() As String
            Return $"Room {RoomNumber} - {RoomType} ({Rate:C2}/night)"
        End Function
    End Class

    Public Class AvailabilityDayInfo
        Public Property [Date] As Date
        Public Property IsAvailable As Boolean
        Public Property Status As String = ""
    End Class

    Public Class AddOnInfo
        Public Property Id As Integer
        Public Property Name As String = ""
        Public Property Description As String = ""
        Public Property Price As Decimal
    End Class

    Public Class SelectedAddOn
        Public Property AddOnId As Integer
        Public Property Quantity As Integer
    End Class

    Public Class AccountInfo
        Public Property Id As Integer
        Public Property FullName As String = ""
        Public Property Email As String = ""
        Public Property Phone As String = ""
        Public Property Username As String = ""
        Public Property Role As String = ""
        Public Property CreatedAt As Date
    End Class

    Public Class ReservationInput
        Public Property RoomId As Integer
        Public Property CheckIn As Date
        Public Property CheckOut As Date
        Public Property AdultGuests As Integer
        Public Property ChildGuests As Integer
        Public Property FreeChildGuests As Integer
        Public Property GuestName As String = ""
        Public Property Email As String = ""
        Public Property Phone As String = ""
        Public Property Address As String = ""
        Public Property PaymentMethod As String = ""
        Public Property PaymentReference As String = ""
        Public Property Notes As String = ""
        Public Property AccountId As Integer
        Public Property AddOns As New List(Of SelectedAddOn)

        Public ReadOnly Property ChargeableGuests As Integer
            Get
                Return AdultGuests + ChildGuests
            End Get
        End Property

        Public ReadOnly Property TotalGuests As Integer
            Get
                Return AdultGuests + ChildGuests + FreeChildGuests
            End Get
        End Property
    End Class

    Public Class AddOnLine
        Public Property Name As String = ""
        Public Property Quantity As Integer
        Public Property UnitPrice As Decimal
        Public Property Total As Decimal
    End Class

    Public Class ReceiptInfo
        Public Property ConfirmationCode As String = ""
        Public Property GuestName As String = ""
        Public Property GuestEmail As String = ""
        Public Property GuestPhone As String = ""
        Public Property RoomNumber As String = ""
        Public Property RoomType As String = ""
        Public Property Amenities As String = ""
        Public Property CheckIn As Date
        Public Property CheckOut As Date
        Public Property Nights As Integer
        Public Property Guests As Integer
        Public Property AdultGuests As Integer
        Public Property ChildGuests As Integer
        Public Property FreeChildGuests As Integer
        Public Property ReservationStatus As String = ""
        Public Property RoomSubtotal As Decimal
        Public Property AddOns As New List(Of AddOnLine)
        Public Property AddOnSubtotal As Decimal
        Public Property Total As Decimal
        Public Property PaymentMethod As String = ""
        Public Property PaymentStatus As String = ""
        Public Property PaymentReference As String = ""
        Public Property CreatedAt As Date
    End Class

    Public Class ReservationHistoryInfo
        Public Property ConfirmationCode As String = ""
        Public Property GuestName As String = ""
        Public Property RoomNumber As String = ""
        Public Property RoomType As String = ""
        Public Property CheckIn As Date
        Public Property CheckOut As Date
        Public Property Guests As Integer
        Public Property AdultGuests As Integer
        Public Property ChildGuests As Integer
        Public Property FreeChildGuests As Integer
        Public Property Status As String = ""
        Public Property Total As Decimal
        Public Property CreatedAt As Date
    End Class

    Public Class NotificationInfo
        Public Property ConfirmationCode As String = ""
        Public Property Channel As String = ""
        Public Property Recipient As String = ""
        Public Property Subject As String = ""
        Public Property Message As String = ""
        Public Property Status As String = ""
        Public Property CreatedAt As Date
    End Class

    Public Class ReservationDetailInfo
        Public Property ConfirmationCode As String = ""
        Public Property RoomId As Integer
        Public Property CheckIn As Date
        Public Property CheckOut As Date
        Public Property AdultGuests As Integer
        Public Property ChildGuests As Integer
        Public Property FreeChildGuests As Integer
        Public Property GuestName As String = ""
        Public Property Email As String = ""
        Public Property Phone As String = ""
        Public Property Address As String = ""
        Public Property PaymentMethod As String = ""
        Public Property PaymentReference As String = ""
        Public Property Notes As String = ""
        Public Property Status As String = ""
        Public Property AddOns As New List(Of SelectedAddOn)
    End Class
End Namespace

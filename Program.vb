Imports System
Imports System.Globalization
Imports System.Windows.Forms
Imports SQLitePCL

Namespace HotelReservation
    Friend Module Program
        <STAThread>
        Public Sub Main()
            Batteries.Init()

            CultureInfo.DefaultThreadCurrentCulture = New CultureInfo("en-PH")
            CultureInfo.DefaultThreadCurrentUICulture = New CultureInfo("en-PH")
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)

            Dim databasePath = IO.Path.Combine(Application.StartupPath, "hotel_reservation.db")
            Dim connectionString = $"Data Source={databasePath}"
            Dim repository = New HotelRepository(connectionString)
            repository.Initialize()

            While True
                Using authForm As New AuthForm(repository)
                    If authForm.ShowDialog() <> DialogResult.OK Then
                        Return
                    End If

                    If String.Equals(authForm.LoggedInAccount.Role, "Admin", StringComparison.OrdinalIgnoreCase) Then
                        Using adminForm As New AdminForm(repository, authForm.LoggedInAccount)
                            adminForm.ShowDialog()
                            If adminForm.LogoutRequested Then
                                Continue While
                            End If
                        End Using
                    Else
                        Using userForm As New MainForm(repository, authForm.LoggedInAccount)
                            userForm.ShowDialog()
                            If userForm.LogoutRequested Then
                                Continue While
                            End If
                        End Using
                    End If
                End Using

                Exit While
            End While
        End Sub
    End Module
End Namespace

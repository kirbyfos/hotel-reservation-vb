Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace HotelReservation
    Public Class AuthForm
        Inherits Form

        Private ReadOnly _repository As HotelRepository
        Private ReadOnly _cream As Color = Color.FromArgb(247, 239, 227)
        Private ReadOnly _linen As Color = Color.FromArgb(255, 250, 241)
        Private ReadOnly _coffee As Color = Color.FromArgb(106, 73, 52)
        Private ReadOnly _espresso As Color = Color.FromArgb(53, 35, 24)
        Private ReadOnly _muted As Color = Color.FromArgb(125, 102, 83)

        Private _loginUsernameText As TextBox
        Private _loginPasswordText As TextBox
        Private _registerFullNameText As TextBox
        Private _registerEmailText As TextBox
        Private _registerPhoneText As TextBox
        Private _registerUsernameText As TextBox
        Private _registerPasswordText As TextBox

        Public Property LoggedInAccount As AccountInfo

        Public Sub New(repository As HotelRepository)
            _repository = repository
            ConfigureWindow()
            BuildLayout()
        End Sub

        Private Sub ConfigureWindow()
            Text = "Casa Reserve - Login"
            StartPosition = FormStartPosition.CenterScreen
            Size = New Size(760, 560)
            FormBorderStyle = FormBorderStyle.FixedSingle
            MaximizeBox = False
            BackColor = _cream
            Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)
        End Sub

        Private Sub BuildLayout()
            Dim root = New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(24),
                .BackColor = _cream
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 92))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(root)

            Dim header = New Label With {
                .Text = "Casa Reserve" & Environment.NewLine & "Login or create a guest account",
                .Dock = DockStyle.Fill,
                .ForeColor = _espresso,
                .Font = New Font("Georgia", 22.0F, FontStyle.Bold, GraphicsUnit.Point)
            }
            root.Controls.Add(header, 0, 0)

            Dim tabs = New TabControl With {.Dock = DockStyle.Fill}
            Dim loginTab = New TabPage("Login") With {.BackColor = _linen, .Padding = New Padding(20)}
            Dim registerTab = New TabPage("Register") With {.BackColor = _linen, .Padding = New Padding(20)}
            tabs.TabPages.Add(loginTab)
            tabs.TabPages.Add(registerTab)
            root.Controls.Add(tabs, 0, 1)

            BuildLoginTab(loginTab)
            BuildRegisterTab(registerTab)
        End Sub

        Private Sub BuildLoginTab(tab As TabPage)
            Dim panel = New TableLayoutPanel With {.Dock = DockStyle.Top, .ColumnCount = 1, .RowCount = 4, .Height = 252}
            For index = 0 To 3
                panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Next
            tab.Controls.Add(panel)

            _loginUsernameText = New TextBox With {.Dock = DockStyle.Fill}
            _loginPasswordText = New TextBox With {.Dock = DockStyle.Fill, .UseSystemPasswordChar = True}

            panel.Controls.Add(WrapField("Username", _loginUsernameText), 0, 0)
            panel.Controls.Add(WrapField("Password", _loginPasswordText), 0, 1)

            Dim loginButton = MakeButton("Login")
            AddHandler loginButton.Click, AddressOf LoginClicked
            panel.Controls.Add(loginButton, 0, 2)

            Dim note = New Label With {
                .Text = "Default admin account: username admin / password admin123",
                .Dock = DockStyle.Fill,
                .ForeColor = _muted
            }
            panel.Controls.Add(note, 0, 3)
        End Sub

        Private Sub BuildRegisterTab(tab As TabPage)
            Dim panel = New TableLayoutPanel With {.Dock = DockStyle.Top, .ColumnCount = 2, .RowCount = 4, .Height = 280}
            panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            For index = 0 To 3
                panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            Next
            tab.Controls.Add(panel)

            _registerFullNameText = New TextBox With {.Dock = DockStyle.Fill}
            _registerEmailText = New TextBox With {.Dock = DockStyle.Fill}
            _registerPhoneText = New TextBox With {.Dock = DockStyle.Fill}
            _registerUsernameText = New TextBox With {.Dock = DockStyle.Fill}
            _registerPasswordText = New TextBox With {.Dock = DockStyle.Fill, .UseSystemPasswordChar = True}

            panel.Controls.Add(WrapField("Full name", _registerFullNameText), 0, 0)
            panel.Controls.Add(WrapField("Email", _registerEmailText), 1, 0)
            panel.Controls.Add(WrapField("Phone", _registerPhoneText), 0, 1)
            panel.Controls.Add(WrapField("Username", _registerUsernameText), 1, 1)
            panel.Controls.Add(WrapField("Password", _registerPasswordText), 0, 2)

            Dim registerButton = MakeButton("Create Account")
            AddHandler registerButton.Click, AddressOf RegisterClicked
            panel.Controls.Add(registerButton, 0, 3)
            panel.SetColumnSpan(registerButton, 2)
        End Sub

        Private Sub LoginClicked(sender As Object, e As EventArgs)
            Dim account = _repository.Authenticate(_loginUsernameText.Text, _loginPasswordText.Text)
            If account Is Nothing Then
                MessageBox.Show("Invalid username or password.", "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            LoggedInAccount = account
            DialogResult = DialogResult.OK
            Close()
        End Sub

        Private Sub RegisterClicked(sender As Object, e As EventArgs)
            Try
                Dim account = _repository.RegisterAccount(
                    _registerFullNameText.Text,
                    _registerEmailText.Text,
                    _registerPhoneText.Text,
                    _registerUsernameText.Text,
                    _registerPasswordText.Text)

                MessageBox.Show("Account created. You can now log in.", "Registration complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
                _loginUsernameText.Text = account.Username
                _loginPasswordText.Clear()
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Registration problem", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Function MakeButton(text As String) As Button
            Return New Button With {
                .Text = text,
                .BackColor = _coffee,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Dock = DockStyle.Fill,
                .Height = 42
            }
        End Function

        Private Function WrapField(labelText As String, control As Control) As Control
            Dim panel = New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(0, 0, 10, 8)}
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
    End Class
End Namespace

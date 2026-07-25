Describe 'Weapon list UI' {
    It 'does not declare a weapon search box or filtering handler' {
        $scriptPath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
        $scriptContent = Get-Content -Raw $scriptPath

        $scriptContent | Should Not Match 'WeaponSearchBox|Get-FilteredWeapons|Add_TextChanged\(\$refreshWeaponList\)'
    }
}

Describe 'Purple dashboard styling' {
    It 'declares the dark glass dashboard resources' {
        $scriptPath = Join-Path $PSScriptRoot '..\\AMacQGuiEditor.ps1'
        $scriptContent = Get-Content -Raw $scriptPath

        $scriptContent | Should Match 'LinearGradientBrush x:Key="PurpleSidebarBrush"'
        $scriptContent | Should Match 'LinearGradientBrush x:Key="PurpleContentBrush"'
        $scriptContent | Should Match '(?s)PurpleSidebarBrush.*?GradientStop Color="#26345E" Offset="0".*?GradientStop Color="#182243" Offset="1"'
        $scriptContent | Should Match '(?s)PurpleContentBrush.*?GradientStop Color="#26345E" Offset="0".*?GradientStop Color="#0B1024" Offset="1"'
        $scriptContent | Should Match 'LinearGradientBrush x:Key="AccentGradientBrush"'
        $scriptContent | Should Match '(?s)AccentGradientBrush.*?GradientStop Color="#22D3EE" Offset="0".*?GradientStop Color="#6366F1" Offset="1"'
        $scriptContent | Should Match 'function Start-AnimatedBackground'
        $scriptContent | Should Match 'Windows\.Media\.Animation\.ColorAnimation'
        $scriptContent | Should Match 'RepeatBehavior\]::Forever'
        $scriptContent | Should Match 'LinearGradientBrush x:Key="PanelSurfaceBrush"'
        $scriptContent | Should Match 'LinearGradientBrush x:Key="InputSurfaceBrush"'
        $scriptContent | Should Match 'LinearGradientBrush x:Key="PopupSurfaceBrush"'
        $scriptContent | Should Match 'Background="\{DynamicResource PopupSurfaceBrush\}"'
        $scriptContent | Should Match '\$list\.Background = \[Windows\.Media\.Brushes\]::Transparent'
        $scriptContent | Should Match "@\{ Key = 'PanelSurfaceBrush';"
        $scriptContent | Should Match "@\{ Key = 'InputSurfaceBrush';"
        $scriptContent | Should Match "@\{ Key = 'PopupSurfaceBrush';"
        $scriptContent | Should Match 'Opacity="0\.46"'
        $scriptContent | Should Match 'Opacity="0\.62"'
        $scriptContent | Should Match 'FromSeconds\(8\)'
        $scriptContent | Should Not Match 'LightFlowOverlay|Windows\.Media\.Animation\.DoubleAnimation|Windows\.Media\.TranslateTransform'
        $scriptContent | Should Match 'Name="ScanlineOverlay"'
        $scriptContent | Should Match 'DrawingBrush TileMode="Tile" Viewport="0,0,1,4"'
        $scriptContent | Should Match 'LineGeometry StartPoint="0,0" EndPoint="1,0"'
        $scriptContent | Should Match 'Opacity="0\.08"'
        $scriptContent | Should Not Match 'Padding="32,22,32,20" Background="\{DynamicResource PanelSurfaceBrush\}"'
        $scriptContent | Should Not Match 'Padding="32,14" Background="\{DynamicResource PanelSurfaceBrush\}"'
        $scriptContent | Should Match '<Setter Property="Background" Value="Transparent"/>'
        $scriptContent | Should Match '\$ctrl\.Background = \[Windows\.Media\.Brushes\]::Transparent'
        $scriptContent | Should Match 'CornerRadius="8" Background="Transparent"'
        $scriptContent | Should Match 'FontSize="13" FontWeight="SemiBold" Foreground="#BDB3DD"\s+Margin="0,0,0,12"'
        $scriptContent | Should Match 'Name="SidebarPanel"'
        $scriptContent | Should Match 'Name="ContentPanel"'
        $scriptContent | Should Match 'Background="{StaticResource PurpleSidebarBrush}"'
        $scriptContent | Should Match 'Background="{StaticResource PurpleContentBrush}"'
        $scriptContent | Should Not Match 'Background="#302650"'
        $scriptContent | Should Not Match '\$bc\.ConvertFromString\(''White''\)'
        $scriptContent | Should Match 'Style x:Key="DarkTextBox" TargetType="TextBox"'
        $scriptContent | Should Match 'Style x:Key="DarkComboBox" TargetType="ComboBox"'
        $scriptContent | Should Match 'Style x:Key="DarkComboBoxItem" TargetType="ComboBoxItem"'
        $scriptContent | Should Match '(?s)Style x:Key="DarkComboBoxItem".*?Trigger Property="IsMouseOver" Value="True".*?Background" Value="#3856B8"'
        $scriptContent | Should Match '(?s)Style x:Key="WeaponListItem".*?Trigger Property="IsMouseOver" Value="True".*?Background" Value="#3856B8"'
        $scriptContent | Should Match '(?s)Style x:Key="DarkComboBoxItem".*?Trigger Property="IsSelected" Value="True".*?Background" Value="\{StaticResource AccentGradientBrush\}".*?Foreground" Value="White"'
        $scriptContent | Should Match '(?s)Style x:Key="WeaponListItem".*?Trigger Property="IsSelected" Value="True".*?Background" Value="\{StaticResource AccentGradientBrush\}".*?Foreground" Value="White"'
        $scriptContent | Should Match '(?s)Style x:Key="WeaponListItem".*?MultiTrigger\.Conditions>.*?Condition Property="IsSelected" Value="True".*?Condition Property="Selector\.IsSelectionActive" Value="False".*?</MultiTrigger\.Conditions>.*?Background" Value="#3856B8"'
        $scriptContent | Should Match 'Style x:Key="DarkScrollThumb" TargetType="Thumb"'
        $scriptContent | Should Match 'Style x:Key="DarkScrollTrackButton" TargetType="RepeatButton"'
        $scriptContent | Should Match 'Style="{StaticResource DarkScrollTrackButton}"'
        $trackButtonStyle = [regex]::Match($scriptContent, '(?s)<Style x:Key="DarkScrollTrackButton".*?</Style>').Value
        $trackButtonStyle | Should Not Match 'IsMouseOver|IsPressed'
        $weaponListItemStyle = [regex]::Match($scriptContent, '(?s)<Style x:Key="WeaponListItem".*?</Style>').Value
        $weaponListItemStyle | Should Match 'Trigger Property="IsMouseOver" Value="True"[\s\S]*?Background" Value="#3856B8"'
        $weaponListItemStyle | Should Not Match '#46366E'
        $comboBoxStyle = [regex]::Match($scriptContent, '(?s)<Style x:Key="DarkComboBox".*?</Style>').Value
        $comboBoxStyle | Should Match 'Trigger Property="IsChecked" Value="True"[\s\S]*?Background" Value="#3856B8"'
        $scrollThumbStyle = [regex]::Match($scriptContent, '(?s)<Style x:Key="DarkScrollThumb".*?</Style>').Value
        $scrollThumbStyle | Should Match 'Background="#5577C8"'
        $scrollThumbStyle | Should Match 'Trigger Property="IsMouseOver" Value="True"[\s\S]*?Background" Value="#71E1FF"'
        $scrollThumbStyle | Should Not Match '#75609F|#9B6CFF'
        $sidebarButtonStyle = [regex]::Match($scriptContent, '(?s)<Style x:Key="SidebarButton".*?</Style>').Value
        $sidebarButtonStyle | Should Match 'Trigger Property="IsPressed" Value="True"[\s\S]*?Background" Value="#2A4FAD"'
        $sidebarButtonStyle | Should Not Match '#5A4788'
        $scriptContent | Should Match 'Style TargetType="ScrollBar"'
        $scriptContent | Should Match 'Style TargetType="ScrollBar"[\s\S]*?Property="Background" Value="#1B315A"'
        $scriptContent | Should Match 'Property="Background" Value="#1B315A"'
        $scriptContent | Should Match 'Background="#5577C8"'
        $scriptContent | Should Match 'Background" Value="{StaticResource AccentGradientBrush}"'
        $scriptContent | Should Match 'Name="SelectedLabel"[^>]*Foreground="#F7F2FF"'
        $scriptContent | Should Match 'Name="SelectedWeaponLabel"[\s\S]*?Foreground="\{StaticResource AccentGradientBrush\}"'
        $scriptContent | Should Match '\$SelectedLabel\.Text = .+; \$SelectedWeaponLabel\.Text = \$Weapon'
        $scriptContent | Should Not Match '\$selectedLbl\.Foreground = New-Object Windows\.Media\.SolidColorBrush'
        $scriptContent | Should Match 'Name="TitleLabel" Text="AMacQ"\s+FontSize="20" FontWeight="SemiBold" Foreground="\{StaticResource AccentGradientBrush\}"'
        $scriptContent | Should Match 'Name="RefreshBtn"[^>]*Foreground="#F7F2FF"'
        $scriptContent | Should Match 'Name="BrowseBtn"[^>]*Foreground="#F7F2FF"'
        $scriptContent | Should Match 'Name="SaveBtn"[^>]*Background="\{StaticResource AccentGradientBrush\}"'
        $scriptContent | Should Match 'Name="SaveBtn"\s+Content="\u5E94\u7528"'
        $scriptContent | Should Match 'TextBlock Text="{Binding Text}"'
        $scriptContent | Should Match 'x:Name="ComboToggleBorder"'
        $scriptContent | Should Match 'x:Name="PART_ContentHost" VerticalContentAlignment="Center"'
        $scriptContent | Should Match '\$ctrl\.CaretBrush = \$bc\.ConvertFromString\(''#5DD7FF''\); \$ctrl\.Padding = ''8,2'''
        $scriptContent | Should Match 'SelectedItem\.Text'
        $scriptContent | Should Match '\[pscustomobject\]@\{ Text = \$_; Value = \$_ \}'
        $scriptContent | Should Match 'function Start-AnimatedBackground'
        $scriptContent | Should Match 'Name="SidebarPanel"'
        $scriptContent | Should Match 'Name="ContentPanel"'
        $scriptContent | Should Match 'RepeatBehavior\]::Forever'
        $scriptContent | Should Match 'Start-AnimatedBackground \$window \$sidebarPanel \$contentPanel'
    }
}

Describe 'Local-only safety boundaries' {
    It 'keeps the editor focused on offline configuration files' {
        $scriptPath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
        $launcherPath = Get-ChildItem (Join-Path $PSScriptRoot '..') -Filter '*.vbs' | Select-Object -First 1 -ExpandProperty FullName
        $scriptContent = Get-Content -Raw $scriptPath
        $launcherContent = Get-Content -Raw $launcherPath

        $scriptContent | Should Match 'Name="LocalOnlyNotice"'
        $scriptContent | Should Not Match 'SendInput|mouse_event|keybd_event|SetWindowsHookEx|RegisterHotKey|OpenProcess|ReadProcessMemory|WriteProcessMemory|CreateRemoteThread'
        $launcherContent | Should Match 'ExecutionPolicy RemoteSigned'
        $launcherContent | Should Not Match 'ExecutionPolicy Bypass'
    }
}

Describe 'Manual configuration file selection' {
    It 'loads two user-selected files without scanning fixed locations or names' {
        $scriptPath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
        $scriptContent = Get-Content -Raw $scriptPath

        $scriptContent | Should Match 'function Read-AMacQConfig \{\s+param\(\[string\]\$KeyBindingsPath, \[string\]\$SensitivityPath\)'
        $scriptContent | Should Match 'New-Object System\.Windows\.Forms\.OpenFileDialog'
        $scriptContent | Should Not Match 'Get-AMacQFolders|Test-AMacQFolder|FolderBrowserDialog|Get-ChildItem C:\\'
        $scriptContent | Should Match '\$browseBtn\.Add_Click\(\$selectConfigFiles\)'
        $scriptContent | Should Not Match '& \$refreshFolders'
        $scriptContent | Should Not Match '\$window\.Title = "AMacQ Configuration Editor -'
    }
}

Describe 'Browser editor entry point' {
    It 'adds a self-contained offline browser editor without removing WPF entry points' {
        $root = Join-Path $PSScriptRoot '..'

        Test-Path (Join-Path $root 'web\index.html') | Should Be $true
        Test-Path (Join-Path $root 'web\styles.css') | Should Be $true
        Test-Path (Join-Path $root 'web\app.js') | Should Be $true
        Test-Path (Join-Path $root 'AMacQGuiEditor.ps1') | Should Be $true
        @(Get-ChildItem -Path $root -Filter '*.vbs' -File).Count | Should Be 2

        $html = Get-Content -Raw (Join-Path $root 'web\index.html')
        $html | Should Match 'id="choose-files"'
        $html | Should Match 'id="weapon-list"'
        $html | Should Match 'id="field-cards"'
        $html | Should Match 'src="app.js"'
    }
}

Describe 'Local browser service' {
    It 'uses only loopback, starts file dialogs from C drive, and keeps file access session-bound' {
        $root = Join-Path $PSScriptRoot '..'
        $serverPath = Join-Path $root 'AMacQWebEditorServer.ps1'
        $launcherPath = Get-ChildItem -Path $root -Filter '*.vbs' -File |
            Where-Object { (Get-Content -Raw $_.FullName) -match 'AMacQWebEditorServer\.ps1' } |
            Select-Object -First 1 -ExpandProperty FullName

        Test-Path $serverPath | Should Be $true
        Test-Path $launcherPath | Should Be $true

        $server = Get-Content -Raw $serverPath
        $launcher = Get-Content -Raw $launcherPath

        $server | Should Match 'http://127\.0\.0\.1:'
        $server | Should Not Match 'http://0\.0\.0\.0:|http://localhost:'
        ([regex]::Matches($server, "InitialDirectory\s*=\s*'C:\\'")).Count | Should Be 2
        $server | Should Match 'Move-Item -Force \$tempPath \$Path'
        $server | Should Match "'index\.html', 'styles\.css', 'app\.js'"
        $server | Should Match 'FromMinutes\(15\)'
        $server | Should Match '\$script:SelectedPaths'
        $server | Should Not Match 'Path\s*=\s*\$body\.'
        $launcher | Should Match 'AMacQWebEditorServer\.ps1'
        $launcher | Should Match '127\.0\.0\.1'
        $launcher | Should Not Match '[^\x00-\x7F]'
    }
}

Describe 'Browser sidebar width' {
    It 'widens the desktop sidebar while preserving the narrow-screen single column layout' {
        $root = Join-Path $PSScriptRoot '..'
        $styles = Get-Content -Raw (Join-Path $root 'web\styles.css')

        $styles | Should Match '\.app-shell\s*\{[\s\S]*?grid-template-columns: 280px minmax\(0, 1fr\);'
        $styles | Should Match '@media \(max-width: 760px\)\s*\{[\s\S]*?\.app-shell\s*\{\s*grid-template-columns: 1fr;\s*\}'
    }
}

Describe 'Browser frosted glass styling' {
    It 'uses layered translucent glass with a readable fallback' {
        $root = Join-Path $PSScriptRoot '..'
        $styles = Get-Content -Raw (Join-Path $root 'web\styles.css')

        $styles | Should Match 'radial-gradient\('
        $styles | Should Match '\.sidebar[\s\S]*?background: rgba\('
        $styles | Should Match '\.content-panel[\s\S]*?background: rgba\('
        $styles | Should Match 'select, input[\s\S]*?background: rgba\('
        $styles | Should Match '@supports \(backdrop-filter: blur\(1px\)\)'
        $styles | Should Match 'backdrop-filter: blur\('
        $styles | Should Match 'body::before[\s\S]*?opacity: \.04'
        $styles | Should Match 'box-shadow:'
    }
}

Option Explicit

Dim shell, fso, basePath, portFile, command, deadline, port, url, stream
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
basePath = fso.GetParentFolderName(WScript.ScriptFullName)
portFile = fso.BuildPath(fso.GetSpecialFolder(2), "AMacQWebEditor-" & Replace(CStr(Timer), ".", "") & ".port")
command = "powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File """ & fso.BuildPath(basePath, "AMacQWebEditorServer.ps1") & """ -PortFile """ & portFile & """"
shell.Run command, 1, False

deadline = DateAdd("s", 10, Now)
Do While Now < deadline
    If fso.FileExists(portFile) Then
        Set stream = fso.OpenTextFile(portFile, 1)
        port = Trim(stream.ReadAll)
        stream.Close
        If Len(port) > 0 Then Exit Do
    End If
    WScript.Sleep 100
Loop

If Len(port) = 0 Then
    MsgBox "Web editor service failed to start. Check PowerShell and local security software.", vbCritical, "AMacQ"
    WScript.Quit 1
End If

url = "http://127.0.0.1:" & port & "/"
shell.Run url, 1, False

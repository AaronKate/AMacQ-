Set shell = CreateObject("WScript.Shell")
folderPath = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
scriptPath = folderPath & "AMacQGuiEditor.ps1"
shell.Run "powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -WindowStyle Hidden -File """ & scriptPath & """", 0, False

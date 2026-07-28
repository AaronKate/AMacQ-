# Task 2 Report: Embed the icon and assign it at runtime

## Scope completed

- `Build-Release.ps1` resolves `assets\AMacQ.ico`, fails early with a localized error if it is missing, and passes it to `Invoke-ps2exe` with `-iconFile`.
- `AMacQGuiEditor.ps1` now uses `Set-WindowIcon` immediately after XAML parsing and before `ShowDialog`.
  - Packaged execution reads the icon embedded in the EXE through `System.Drawing.Icon::ExtractAssociatedIcon` and converts it to a WPF `ImageSource`.
  - Development execution falls back to the local `assets\AMacQ.ico` file.
- No root `Window.Icon` XAML attribute was added: an external asset reference would fail in the required single-file EXE distribution.
- `tests/BuildRelease.Tests.ps1` now checks the build validation and `-iconFile` integration, the runtime helper, its EXE-first/local-fallback behavior, invocation ordering, and absence of an external XAML icon reference.

## TDD evidence

1. Added the focused static assertions to `tests/BuildRelease.Tests.ps1`.
2. Ran the test before production changes; it failed as expected with: `The build script must resolve assets\AMacQ.ico.`
3. Added the minimum build and runtime implementation.
4. Ran all requested tests successfully.

## Validation

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\BuildRelease.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\IconResource.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\TitleBarLayout.Tests.ps1
```

All three commands exited successfully. `IconResource.Tests.ps1` regenerated and validated the ICO resource, and `TitleBarLayout.Tests.ps1` successfully parsed the Window XAML.

## Configuration behavior

No configuration parsing, editing, validation, persistence, or save behavior was changed.

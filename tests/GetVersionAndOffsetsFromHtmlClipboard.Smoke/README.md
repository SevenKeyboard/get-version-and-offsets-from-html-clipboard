# Smoke and original-AHK comparison

The smoke tests use fixed numeric expectations captured from the original AHK
v1.1.37.02 parser on x64 and x86. `NumericCases.tsv` stores each input as UTF-16
code-unit hex followed by its expected signed decimal output. Tests cover all
six offsets, partial headers, duplicates, line limits, CR handling, missing and
empty values, and real UTF-8 fragment byte positions.

Optional oracle mode reads the supplied original source, changes only the
misspelled metadata initializer reference in a GUID-named temporary copy, and
runs the unchanged parser body. It compares every result with C# and removes
its temporary files. It never accesses the clipboard or displays a UI.

```powershell
dotnet run --project tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke -c Release -- --oracle "C:/Program Files/AutoHotkey/v1.1.37.02/AutoHotkeyU64.exe" "../main-ahkv1.1/getVersionAndOffsetsFromHtmlClipboard.ahk"
dotnet build tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke -c Release -r win-x86 -p:PlatformTarget=x86
& "C:/Program Files (x86)/dotnet/dotnet.exe" "tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke/bin/Release/net10.0-windows/win-x86/GetVersionAndOffsetsFromHtmlClipboard.Smoke.dll" --oracle "C:/Program Files/AutoHotkey/v1.1.37.02/AutoHotkeyU32.exe" "../main-ahkv1.1/getVersionAndOffsetsFromHtmlClipboard.ahk"
dotnet publish tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke -c Release -r win-x64 -p:PublishAot=true
dotnet publish tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke -c Release -r win-x86 -p:PlatformTarget=x86 -p:PublishAot=true
```

Run each published executable from its `bin/Release/net10.0-windows/<rid>/publish`
directory. Missing fields are compared with AHK's empty outputs; separate smoke
assertions preserve the C# distinction between an absent Version and `Version:`.

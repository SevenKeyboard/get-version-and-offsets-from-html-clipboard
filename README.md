# get-version-and-offsets-from-html-clipboard

Parses the first ten lines of a CF_HTML string on Windows 10+ (.NET 10).

```csharp
using SevenKeyboard;

HtmlClipboardHeader header = GetVersionAndOffsetsFromHtmlClipboard.Parse(html);
if (header.StartFragment is long start && header.EndFragment is long end)
    Console.WriteLine($"Fragment bytes: {start}..{end}");
```

Missing fields are null; a present empty Version remains empty. Names ignore ASCII case,
and the last duplicate within the first ten LF-delimited lines wins. Only CR at
line boundaries is trimmed. Offsets are UTF-8 byte positions, not string indexes;
the parser does not validate their bounds or order.

Offset conversion preserves AHK string semantics: `12abc` becomes 12, `1e3`
becomes 1, `0x10` becomes 16, empty/nonnumeric values become 0, and overflow
saturates at signed 64-bit limits. Version 2.0.0 returns a nullable-field result.
Supports x64, x86 and NativeAOT; uses the Windows CRT from System32. No clipboard access.

```powershell
dotnet run --project tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke -c Release
```

See [oracle verification](tests/GetVersionAndOffsetsFromHtmlClipboard.Smoke/README.md) for comparison with the original AHK parser.

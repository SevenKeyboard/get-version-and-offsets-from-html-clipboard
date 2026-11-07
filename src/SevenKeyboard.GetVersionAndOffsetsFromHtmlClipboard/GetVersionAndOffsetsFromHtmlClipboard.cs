//==============================================================
// getVersionAndOffsetsFromHtmlClipboard — Parses CF_HTML header fields (Version and fragment/selection offsets)
//
// GitHub: https://github.com/SevenKeyboard/get-version-and-offsets-from-html-clipboard
// Author: SevenKeyboard Ltd. (2026)
// License: The Unlicense
//==============================================================

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SevenKeyboard;

public readonly record struct HtmlClipboardHeader(
    string? Version,
    long? StartHtml,
    long? EndHtml,
    long? StartFragment,
    long? EndFragment,
    long? StartSelection,
    long? EndSelection);

public static partial class GetVersionAndOffsetsFromHtmlClipboard
{
    public static HtmlClipboardHeader Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        ReadOnlySpan<char> text = html.AsSpan();
        int terminator = text.IndexOf('\0');
        if (terminator >= 0)
            text = text[..terminator];

        string? version = null;
        long? startHtml = null, endHtml = null, startFragment = null, endFragment = null;
        long? startSelection = null, endSelection = null;
        int lineCount = 0;
        foreach (Range range in text.Split('\n'))
        {
            if (++lineCount > 10)
                break;

            ReadOnlySpan<char> line = text[range].Trim('\r');
            int colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            string key = line[..colon].ToString();
            // AHK's default StringCaseSense ignores ASCII case only.
            if (!Ascii.IsValid(key))
                continue;

            string value = line[(colon + 1)..].ToString();
            switch (key.ToUpperInvariant())
            {
                case "VERSION": version = value; break;
                case "STARTHTML": startHtml = ParseOffset(value); break;
                case "ENDHTML": endHtml = ParseOffset(value); break;
                case "STARTFRAGMENT": startFragment = ParseOffset(value); break;
                case "ENDFRAGMENT": endFragment = ParseOffset(value); break;
                case "STARTSELECTION": startSelection = ParseOffset(value); break;
                case "ENDSELECTION": endSelection = ParseOffset(value); break;
            }
        }

        return new(version, startHtml, endHtml, startFragment, endFragment, startSelection, endSelection);
    }

    private static long ParseOffset(string value)
    {
        // AHK's string-to-integer formatting reads a numeric prefix, returns zero
        // without digits, and saturates on overflow. Leading zeros remain decimal.
        ReadOnlySpan<char> prefix = value.AsSpan().TrimStart(" \t");
        if (!prefix.IsEmpty && prefix[0] is '+' or '-')
            prefix = prefix[1..];

        bool hexadecimal = prefix.Length >= 3 && prefix[0] == '0'
            && prefix[1] is 'x' or 'X' && char.IsAsciiHexDigit(prefix[2]);
        return ParseIntegerNative(value, 0, hexadecimal ? 16 : 10);
    }

    // The Windows 10+ CRT preserves the original wide-character numeric conversion.
    [LibraryImport("ucrtbase.dll", EntryPoint = "_wcstoi64", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial long ParseIntegerNative(string value, nint end, int numberBase);
}

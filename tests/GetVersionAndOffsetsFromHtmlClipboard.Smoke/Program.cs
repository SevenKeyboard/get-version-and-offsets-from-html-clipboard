using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SevenKeyboard;

internal static class Program
{
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            List<(string Name, string Input, HtmlClipboardHeader Expected)> cases = CreateCases();
            foreach (var test in cases)
                Check(GetVersionAndOffsetsFromHtmlClipboard.Parse(test.Input) == test.Expected, test.Name);

            bool nullRejected = false;
            try
            {
                GetVersionAndOffsetsFromHtmlClipboard.Parse(null!);
            }
            catch (ArgumentNullException)
            {
                nullRejected = true;
            }
            Check(nullRejected, "Null input is rejected.");
            Check(GetVersionAndOffsetsFromHtmlClipboard.Parse("") == default,
                "An empty parse does not retain previous fields.");

            if (args.Length == 3 && args[0] == "--oracle")
                CompareOracle(cases, args[1], args[2]);
            else if (args.Length != 0)
                throw new ArgumentException("Usage: [--oracle <AutoHotkey.exe> <original.ahk>]");

            Console.WriteLine($"PASS: {checks} checks ({cases.Count} parser cases), {RuntimeInformation.ProcessArchitecture}.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static List<(string Name, string Input, HtmlClipboardHeader Expected)> CreateCases()
    {
        List<(string Name, string Input, HtmlClipboardHeader Expected)> cases =
        [
            ("Empty input", "", default),
            ("Unknown fields", "SourceURL:https://example.test/a:b\n<html>text</html>", default),
            ("Version only", "Version:1.0", new() { Version = "1.0" }),
            ("Empty version", "Version:", new() { Version = "" }),
            ("Version is verbatim", "Version:  1.0:extra\t ", new() { Version = "  1.0:extra\t " }),
            ("Unicode version", "Version:日本語😀", new() { Version = "日本語😀" }),
            ("Full CRLF header", "Version:1.0\r\nStartHTML:00000100\r\nEndHTML:250\r\nStartFragment:120\r\nEndFragment:220\r\nStartSelection:125\r\nEndSelection:210\r\n",
                new("1.0", 100, 250, 120, 220, 125, 210)),
            ("Last duplicate wins", "Version:0.9\nVersion:1.0\nStartHTML:12\nStartHTML:30",
                new() { Version = "1.0", StartHtml = 30 }),
            ("Empty duplicate overwrites version", "Version:1.0\nVersion:", new() { Version = "" }),
            ("Invalid duplicate overwrites number", "StartHTML:50\nStartHTML:garbage", new() { StartHtml = 0 }),
            ("Empty duplicate overwrites number", "StartHTML:50\nStartHTML:", new() { StartHtml = 0 }),
            ("Default case-insensitive names", "version:x\nStArThTmL:42\nendhtml:99\nstartfragment:3\nendfragment:4\nstartselection:5\nendselection:6",
                new("x", 42, 99, 3, 4, 5, 6)),
            ("Long-s is not ASCII s", "Verſion:1", default),
            ("Dotless i is not ASCII i", "versıon:1", default),
            ("Dotted I is not ASCII I", "verSİon:1", default),
            ("Fullwidth keys are not recognized", "ｖｅｒｓｉｏｎ:1", default),
            ("Long-s offset key is not recognized", "ſtartHTML:1", default),
            ("Lookalike duplicate does not overwrite", "Version:x\nVerſion:y", new() { Version = "x" }),
            ("Keys are not space-trimmed", " Version:no\nStartHTML :5\n\tEndHTML:8", default),
            ("CR trimmed at both ends", "\r\rVersion:x\r\r\n\rStartHTML:12\r", new() { Version = "x", StartHtml = 12 }),
            ("Internal CR is preserved", "Version:x\ry\nStartHTML:12\r34", new() { Version = "x\ry", StartHtml = 12 }),
            ("Lone CR is not a separator", "Version:x\rStartHTML:12", new() { Version = "x\rStartHTML:12" }),
            ("First colon only", "StartHTML:12:99\nVersion:x:y", new() { Version = "x:y", StartHtml = 12 }),
            ("Tenth line included", new string('\n', 9) + "StartHTML:10", new() { StartHtml = 10 }),
            ("Eleventh line ignored", new string('\n', 10) + "StartHTML:11", default),
            ("Later duplicate beyond limit ignored", "StartHTML:1" + new string('\n', 10) + "StartHTML:11", new() { StartHtml = 1 }),
            ("Unknown lines count toward limit", string.Concat(System.Linq.Enumerable.Repeat("Unknown:x\n", 10)) + "StartHTML:11", default),
            ("No early HTML-body stop", "<html>\n\nStartHTML:7", new() { StartHtml = 7 }),
            ("Offsets are not validated", "StartHTML:-1\nEndHTML:-2\nStartFragment:999999\nEndFragment:3",
                new() { StartHtml = -1, EndHtml = -2, StartFragment = 999999, EndFragment = 3 }),
            ("BOM is not trimmed", "\uFEFFVersion:x\nStartHTML:3", new() { StartHtml = 3 }),
            ("NUL terminates original string", "Version:x\0\nStartHTML:5", new() { Version = "x" }),
            ("Only CR is omitted", "\r Version:no\r\nVersion: x \r", new() { Version = " x " })
        ];

        string[] keys = ["StartHTML", "EndHTML", "StartFragment", "EndFragment", "StartSelection", "EndSelection"];
        string fixture = Path.Combine(AppContext.BaseDirectory, "NumericCases.tsv");
        foreach (string row in File.ReadLines(fixture))
        {
            if (row.StartsWith('#'))
                continue;
            string[] columns = row.Split('\t');
            string value = DecodeHex(columns[0]);
            long expected = long.Parse(columns[1], CultureInfo.InvariantCulture);
            for (int field = 0; field < keys.Length; field++)
            {
                HtmlClipboardHeader header = field switch
                {
                    0 => new() { StartHtml = expected },
                    1 => new() { EndHtml = expected },
                    2 => new() { StartFragment = expected },
                    3 => new() { EndFragment = expected },
                    4 => new() { StartSelection = expected },
                    _ => new() { EndSelection = expected }
                };
                cases.Add(($"{keys[field]} numeric input {columns[0]}", keys[field] + ":" + value, header));
            }
        }

        const string before = "<html><body>앞😀<!--StartFragment-->";
        const string fragment = "한글é😀";
        const string after = "<!--EndFragment--></body></html>";
        string template = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        int startHtml = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, template, 0, 0, 0, 0));
        int startFragment = startHtml + Encoding.UTF8.GetByteCount(before);
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        int endHtml = endFragment + Encoding.UTF8.GetByteCount(after);
        string html = string.Format(CultureInfo.InvariantCulture, template, startHtml, endHtml, startFragment, endFragment)
            + before + fragment + after;
        cases.Add(("Real UTF-8 byte positions", html, new("1.0", startHtml, endHtml, startFragment, endFragment, null, null)));
        HtmlClipboardHeader parsed = GetVersionAndOffsetsFromHtmlClipboard.Parse(html);
        byte[] bytes = Encoding.UTF8.GetBytes(html);
        Check(Encoding.UTF8.GetString(bytes, (int)parsed.StartFragment!.Value,
            (int)(parsed.EndFragment!.Value - parsed.StartFragment.Value)) == fragment, "UTF-8 fragment round trip.");
        Check(parsed.StartFragment != html.IndexOf(fragment, StringComparison.Ordinal), "Byte positions differ from UTF-16 indexes.");
        return cases;
    }

    private static void CompareOracle(
        List<(string Name, string Input, HtmlClipboardHeader Expected)> cases,
        string executable, string originalPath)
    {
        string source = File.ReadAllText(originalPath);
        const string typo = "VersionManager_VersionManager_getVersionAndOffsetsFromHtmlClipboardMonitorExGetUtils._init()";
        Check(source.Contains(typo, StringComparison.Ordinal), "Expected original metadata typo exists.");
        // Only metadata is adapted. The original function body is appended unchanged.
        string adapted = source.Replace(typo, "VersionManager_getVersionAndOffsetsFromHtmlClipboard._init()", StringComparison.Ordinal);
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string directory = Path.GetFullPath(Path.Combine(tempRoot, "html-header-oracle-" + Guid.NewGuid().ToString("N")));
        Check(directory.StartsWith(Path.TrimEndingDirectorySeparator(tempRoot) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Oracle temporary path stays inside the temporary directory.");
        Directory.CreateDirectory(directory);
        try
        {
            string script = Path.Combine(directory, "oracle.ahk");
            string input = Path.Combine(directory, "input.txt");
            string output = Path.Combine(directory, "output.txt");
            File.WriteAllText(script, OracleHarness + Environment.NewLine + adapted, new UTF8Encoding(true));
            List<string> inputs = new();
            foreach (var test in cases)
                inputs.Add(EncodeHex(test.Input));
            File.WriteAllText(input, string.Join("\n", inputs), new UTF8Encoding(false));

            ProcessStartInfo start = new(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("/ErrorStdOut");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(input);
            start.ArgumentList.Add(output);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start AHK oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill();
                process.WaitForExit();
                throw new TimeoutException("AHK oracle timed out.");
            }
            Check(process.ExitCode == 0, $"Oracle exit: {stdout.GetAwaiter().GetResult()} {stderr.GetAwaiter().GetResult()}");
            string[] actual = File.ReadAllLines(output);
            Check(actual.Length == cases.Count, $"Oracle returned {actual.Length} rows for {cases.Count} cases.");
            for (int i = 0; i < cases.Count; i++)
            {
                HtmlClipboardHeader header = GetVersionAndOffsetsFromHtmlClipboard.Parse(cases[i].Input);
                string expected = EncodeHex(header.Version ?? "") + "|"
                    + Number(header.StartHtml) + "|" + Number(header.EndHtml) + "|"
                    + Number(header.StartFragment) + "|" + Number(header.EndFragment) + "|"
                    + Number(header.StartSelection) + "|" + Number(header.EndSelection);
                Check(actual[i] == expected, $"Oracle mismatch for {cases[i].Name}: AHK={actual[i]}, C#={expected}");
            }
            Console.WriteLine($"Oracle PASS: {cases.Count} cases using {Path.GetFileName(executable)}; metadata-only adapter.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static string EncodeHex(string text)
    {
        StringBuilder result = new();
        foreach (char character in text)
            result.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static string DecodeHex(string text)
    {
        StringBuilder result = new();
        for (int i = 0; i < text.Length; i += 4)
            result.Append((char)ushort.Parse(text.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        checks++;
    }

    private const string OracleHarness = """
        #NoTrayIcon
        #SingleInstance Off
        SetBatchLines, -1
        FileRead, input, % A_Args[1]
        output := ""
        loop Parse, input, `n, `r
        {
            encoded := A_LoopField
            units := StrLen(encoded) // 4
            VarSetCapacity(buffer, (units + 1) * 2, 0)
            loop % units
                NumPut("0x" . SubStr(encoded, (A_Index - 1) * 4 + 1, 4), buffer, (A_Index - 1) * 2, "UShort")
            value := StrGet(&buffer, "UTF-16")
            getVersionAndOffsetsFromHtmlClipboard(value, version, startHTML, endHTML, startFragment, endFragment, startSelection, endSelection)
            output .= Hex(version) . "|" . startHTML . "|" . endHTML . "|" . startFragment . "|" . endFragment . "|" . startSelection . "|" . endSelection . "`n"
        }
        FileAppend, % output, % A_Args[2], UTF-8-RAW
        ExitApp

        Hex(value) {
            output := ""
            loop % StrLen(value)
                output .= Format("{:04X}", NumGet(&value, (A_Index - 1) * 2, "UShort"))
            return output
        }
        """;
}

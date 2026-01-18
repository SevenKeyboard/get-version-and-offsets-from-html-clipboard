#Requires AutoHotkey v1.1.37+
;==============================================================
; getVersionAndOffsetsFromHtmlClipboard — Parses CF_HTML header fields (Version and fragment/selection offsets)
;
; GitHub: https://github.com/SevenKeyboard/get-version-and-offsets-from-html-clipboard
; Author: SevenKeyboard Ltd. (2026)
; License: The Unlicense
;==============================================================
class VersionManager_getVersionAndOffsetsFromHtmlClipboard
{
    static _ := VersionManager_VersionManager_getVersionAndOffsetsFromHtmlClipboardMonitorExGetUtils._init()
    _init()    {
        global
        GETVERSIONANDOFFSETSFROMHTMLCLIPBOARD_VERSION := "1.0.0"
    }
}
getVersionAndOffsetsFromHtmlClipboard(html, byRef version:="", byRef startHTML:="", byRef endHTML:="", byRef startFragment:="", byRef endFragment:="", byRef startSelection:="", byRef endSelection:="")    { ;  ahk1.1
    version:= startHTML:= endHTML:= startFragment:= endFragment:= startSelection:= endSelection:= ""
    loop Parse, % html, `n, `r
    {
        if (10 < A_Index)
            break
        if (inStr(A_LoopField, ":"))    {
            str := strSplit(A_LoopField, ":",, 2)
            if (str.length() !== 2)
                continue
            switch (str[1])
            {
                case "Version":                 version         := str[2]
                case "StartHTML":               startHTML       := format("{:d}", str[2])
                case "EndHTML":                 endHTML         := format("{:d}", str[2])
                case "StartFragment":           startFragment   := format("{:d}", str[2])
                case "EndFragment":             endFragment     := format("{:d}", str[2])
                case "StartSelection":          startSelection  := format("{:d}", str[2])
                case "EndSelection":            endSelection    := format("{:d}", str[2])
            }
        }
    }
}
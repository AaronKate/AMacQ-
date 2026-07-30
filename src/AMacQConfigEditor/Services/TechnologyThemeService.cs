using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AMacQConfigEditor.Services;

internal static class TechnologyThemeService
{
    private static readonly TechnologyTheme[] Themes =
    [
        Create("Deep Ocean", "#142C48", "#081522", "#1C4268", "#10263D", "#3ED8FF", "#7687FF", "#1A3654", "#101C30", "#193653", "#0B1728", "#1A3857", "#0D2037", "#142A49", "#0B1B32", "#132A48", "#081728", "#4D91B3", "#6CE1FF", "#304E6F", "#6B9CCE", "#245575", "#183D5A"),
        Create("Quantum Violet", "#251B4B", "#0E0A26", "#3D2D70", "#191037", "#D48CFF", "#7B8CFF", "#33275B", "#191231", "#302550", "#120D29", "#34295A", "#171031", "#2A214A", "#120C28", "#271F47", "#100B24", "#8864B8", "#DEA8FF", "#4E3D78", "#A47DDB", "#513A82", "#37265F"),
        Create("Carbon Amber", "#302B2A", "#141313", "#554742", "#29211F", "#FFB45B", "#D3743D", "#39302C", "#211B19", "#342B28", "#1A1514", "#3A302C", "#201918", "#302724", "#1B1514", "#2C2421", "#171211", "#B58A5B", "#FFD08A", "#70503A", "#CB9358", "#785035", "#523525"),
        Create("Matrix Emerald", "#0D352F", "#061916", "#165348", "#0B2D27", "#44F3C4", "#22B890", "#123D35", "#09231F", "#10382F", "#071C19", "#113C33", "#08251F", "#0D332C", "#061C18", "#0C3028", "#051613", "#3A9C86", "#78FFD9", "#276457", "#4AC8A8", "#185B4C", "#0E3D34")
    ];

    public static string ApplyRandomTheme(Window window)
    {
        var theme = Themes[Random.Shared.Next(Themes.Length)];
        foreach (var (key, value) in theme.Colors)
        {
            window.Resources[key] = (Color)ColorConverter.ConvertFromString(value)!;
        }

        return theme.Name;
    }

    private static TechnologyTheme Create(
        string name, string appStart, string appEnd, string titleStart, string titleEnd,
        string accentStart, string accentEnd, string sidebarStart, string sidebarEnd,
        string contentStart, string contentEnd, string panelStart, string panelEnd,
        string inputStart, string inputEnd, string popupStart, string popupEnd,
        string border, string focus, string divider, string panelBorder, string hover, string pressed) =>
        new(name, new Dictionary<string, string>
        {
            ["SurfaceAppStartColor"] = appStart, ["SurfaceAppEndColor"] = appEnd,
            ["SurfaceTitleStartColor"] = titleStart, ["SurfaceTitleEndColor"] = titleEnd,
            ["AccentCyanColor"] = accentStart, ["AccentIndigoColor"] = accentEnd,
            ["SurfaceSidebarStartColor"] = sidebarStart, ["SurfaceSidebarEndColor"] = sidebarEnd,
            ["SurfaceContentStartColor"] = contentStart, ["SurfaceContentEndColor"] = contentEnd,
            ["SurfacePanelStartColor"] = panelStart, ["SurfacePanelEndColor"] = panelEnd,
            ["SurfaceInputStartColor"] = inputStart, ["SurfaceInputEndColor"] = inputEnd,
            ["SurfacePopupStartColor"] = popupStart, ["SurfacePopupEndColor"] = popupEnd,
            ["BorderControlColor"] = border, ["BorderFocusColor"] = focus,
            ["BorderDividerColor"] = divider, ["BorderPanelColor"] = panelBorder,
            ["ControlHoverColor"] = hover, ["ControlPressedColor"] = pressed,
            ["ScrollTrackColor"] = inputEnd, ["ScrollThumbColor"] = panelBorder,
            ["ScrollThumbHoverColor"] = focus
        });

    private sealed record TechnologyTheme(string Name, IReadOnlyDictionary<string, string> Colors);
}

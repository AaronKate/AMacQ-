using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AMacQConfigEditor.Services;

internal static class TechnologyThemeService
{
    private static readonly Random ThemeRandom = new Random();

    private static readonly TechnologyTheme[] Themes =
    [
        Create("Deep Ocean", "#142C48", "#081522", "#1C4268", "#10263D", "#3ED8FF", "#7687FF", "#1A3654", "#101C30", "#193653", "#0B1728", "#1A3857", "#0D2037", "#142A49", "#0B1B32", "#132A48", "#081728", "#4D91B3", "#6CE1FF", "#304E6F", "#6B9CCE", "#245575", "#183D5A"),
        Create("Quantum Violet", "#251B4B", "#0E0A26", "#3D2D70", "#191037", "#D48CFF", "#7B8CFF", "#33275B", "#191231", "#302550", "#120D29", "#34295A", "#171031", "#2A214A", "#120C28", "#271F47", "#100B24", "#8864B8", "#DEA8FF", "#4E3D78", "#A47DDB", "#513A82", "#37265F"),
        Create("Carbon Amber", "#302B2A", "#141313", "#554742", "#29211F", "#FFB45B", "#D3743D", "#39302C", "#211B19", "#342B28", "#1A1514", "#3A302C", "#201918", "#302724", "#1B1514", "#2C2421", "#171211", "#B58A5B", "#FFD08A", "#70503A", "#CB9358", "#785035", "#523525"),
        Create("Matrix Emerald", "#0D352F", "#061916", "#165348", "#0B2D27", "#44F3C4", "#22B890", "#123D35", "#09231F", "#10382F", "#071C19", "#113C33", "#08251F", "#0D332C", "#061C18", "#0C3028", "#051613", "#3A9C86", "#78FFD9", "#276457", "#4AC8A8", "#185B4C", "#0E3D34"),
        Create("Midnight Azure", "#101B36", "#050914", "#172A56", "#0B1430", "#5EB5FF", "#5672FF", "#162447", "#0A1126", "#172B54", "#080E20", "#17294D", "#0B1329", "#142542", "#081124", "#122441", "#060D1F", "#5278AF", "#92D2FF", "#2C4670", "#719DDB", "#213E6B", "#142D52"),
        Create("Neon Magenta", "#351030", "#160914", "#59183E", "#250C22", "#FF6FCE", "#9A77FF", "#4A1740", "#200C1C", "#47183F", "#1C0A1A", "#4E1945", "#230D20", "#401438", "#1E0B1B", "#3D1235", "#190915", "#AF528F", "#FF9EDF", "#71315D", "#E276B6", "#783062", "#552143"),
        Create("Titanium Slate", "#28323A", "#11171D", "#3A4A57", "#1A252E", "#86D5F5", "#91A7FF", "#34414B", "#192128", "#303F4C", "#141C24", "#34434E", "#1A222A", "#2E3B45", "#182027", "#2A3741", "#141B21", "#7995A8", "#B8EBFF", "#4C626F", "#9CB7C7", "#3C5665", "#2A404E"),
        Create("Lava Red", "#421817", "#1C0909", "#6B2823", "#32100F", "#FF8765", "#FFBC57", "#56201C", "#260C0B", "#511D1A", "#210A0A", "#59231E", "#2B100E", "#481B18", "#240C0B", "#431915", "#1E0909", "#B25D52", "#FFC09D", "#743630", "#E67B68", "#7F302B", "#5C211D"),
        Create("Aurora Cyan", "#083845", "#03171E", "#0E5968", "#062A35", "#50F4EB", "#55A7FF", "#0B4B57", "#05232C", "#0B4652", "#041D25", "#0D4A56", "#06262E", "#0A4050", "#052129", "#093D49", "#031B22", "#3B9EAA", "#8FFFF5", "#236A77", "#52C8D6", "#186B79", "#0C4856"),
        Create("Nebula Indigo", "#1C1A4C", "#09091F", "#343080", "#151239", "#9CA7FF", "#D07AFF", "#29266B", "#12102F", "#292661", "#0E0D28", "#2D2A6A", "#151231", "#25235A", "#10102C", "#222052", "#0B0B24", "#7778BE", "#C5C8FF", "#444477", "#9A93DE", "#48417E", "#312B61"),
        Create("Quantum Teal", "#073733", "#021916", "#0B5A52", "#052B27", "#69F5BD", "#56C8FF", "#0A4A43", "#042522", "#0B4640", "#031E1B", "#0D4B44", "#062823", "#093E3A", "#04211E", "#083A35", "#021A18", "#3A9C8C", "#9EFFE1", "#286B62", "#59CDB8", "#176D61", "#0C4C44"),
        Create("Dark Copper", "#3B2118", "#180D09", "#60351F", "#2C180E", "#FFB06B", "#E37C4B", "#512E20", "#24140D", "#4B2A1D", "#1E100B", "#523020", "#29170F", "#432619", "#21120C", "#3E2317", "#1B0E09", "#B27A50", "#FFD09A", "#72442C", "#D99A64", "#7B452A", "#572F1C"),
    ];

    public static string ApplyRandomTheme(Window window)
    {
        var theme = Themes[ThemeRandom.Next(Themes.Length)];
        foreach (var color in theme.Colors)
        {
            window.Resources[color.Key] = (Color)ColorConverter.ConvertFromString(color.Value)!;
        }

        return theme.Name;
    }

    private static TechnologyTheme Create(
        string name, string appStart, string appEnd, string titleStart, string titleEnd,
        string accentStart, string accentEnd, string sidebarStart, string sidebarEnd,
        string contentStart, string contentEnd, string panelStart, string panelEnd,
        string inputStart, string inputEnd, string popupStart, string popupEnd,
        string border, string focus, string divider, string panelBorder, string hover, string pressed,
        string textPrimary = "#F7F2FF", string textBody = "#EDE7FF", string textSecondary = "#B9CAE0", string textList = "#DCEBFA") =>
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
            ["TextPrimaryColor"] = textPrimary, ["TextBodyColor"] = textBody,
            ["TextSecondaryColor"] = textSecondary, ["TextListColor"] = textList,
            ["BorderControlColor"] = border, ["BorderFocusColor"] = focus,
            ["BorderDividerColor"] = divider, ["BorderPanelColor"] = panelBorder,
            ["ControlHoverColor"] = hover, ["ControlPressedColor"] = pressed,
            ["ScrollTrackColor"] = inputEnd, ["ScrollThumbColor"] = panelBorder,
            ["ScrollThumbHoverColor"] = focus
        });

    private sealed record TechnologyTheme(string Name, IReadOnlyDictionary<string, string> Colors);
}

using MudBlazor;

namespace PadelHub.Theme;

/// <summary>
/// Tema visual PadelHub: "Court Glass & Floodlight".
///
/// Diambil dari dunia padel itu sendiri — lapangan berdinding kaca, permukaan
/// biru-teal, garis servis putih, dan bola optic lime di bawah lampu sorot.
/// Mode terang = lapangan siang hari (kaca pucat, tinta teal pekat).
/// Mode gelap  = lapangan malam di bawah floodlight.
///
/// Nilai lime (--pd-ball) sengaja TIDAK dipakai sebagai warna MudBlazor Secondary
/// karena kontrasnya buruk di permukaan terang; lime hanya dipakai di atas
/// permukaan gelap (drawer, plat skor, hero) lewat CSS.
/// </summary>
public static class PadelHubTheme
{
    // PENTING: field ini harus dideklarasikan sebelum Instance. Inisialisasi
    // field statis berjalan sesuai urutan penulisan, jadi kalau ditaruh di
    // bawah, Instance akan memakai nilai null dan font-family tidak terpasang.
    private static readonly string[] DisplayStack = ["Archivo", "Instrument Sans", "Segoe UI", "system-ui", "sans-serif"];
    private static readonly string[] BodyStack = ["Instrument Sans", "Segoe UI", "system-ui", "-apple-system", "sans-serif"];
    private static readonly string[] MonoStack = ["IBM Plex Mono", "Consolas", "SFMono-Regular", "monospace"];

    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0E7A85",
            PrimaryDarken = "#0A5C64",
            PrimaryLighten = "#4FA8AF",
            PrimaryContrastText = "#FFFFFF",

            Secondary = "#E4571F",
            SecondaryDarken = "#C2440F",
            SecondaryLighten = "#F2814F",
            SecondaryContrastText = "#FFFFFF",

            Tertiary = "#0B3B45",
            TertiaryContrastText = "#FFFFFF",

            Info = "#1E88A8",
            Success = "#2E9E5B",
            Warning = "#DE8C0A",
            Error = "#D64027",
            Dark = "#082A31",

            Background = "#EDF2F2",
            BackgroundGray = "#E3EBEB",
            Surface = "#FFFFFF",

            AppbarBackground = "rgba(255,255,255,0.72)",
            AppbarText = "#082A31",

            DrawerBackground = "#072A30",
            DrawerText = "#C7DDDF",
            DrawerIcon = "#8FB3B7",

            TextPrimary = "#082A31",
            TextSecondary = "#52737A",
            TextDisabled = "rgba(8,42,49,0.38)",

            ActionDefault = "#52737A",
            ActionDisabled = "rgba(8,42,49,0.26)",
            ActionDisabledBackground = "rgba(8,42,49,0.10)",

            Divider = "#DCE6E7",
            DividerLight = "#E8F0F0",
            LinesDefault = "#DCE6E7",
            LinesInputs = "#BFD3D5",
            TableLines = "#E4EDEE",
            TableStriped = "rgba(14,122,133,0.035)",
            TableHover = "rgba(14,122,133,0.06)",

            Skeleton = "rgba(8,42,49,0.08)",
        },

        PaletteDark = new PaletteDark
        {
            Primary = "#35B5BC",
            PrimaryDarken = "#1D8F97",
            PrimaryLighten = "#6FD3D8",
            PrimaryContrastText = "#03181B",

            Secondary = "#FF8552",
            SecondaryDarken = "#E4571F",
            SecondaryLighten = "#FFA982",
            SecondaryContrastText = "#1A0A03",

            Tertiary = "#9FE7EC",
            TertiaryContrastText = "#03181B",

            Info = "#4FB6D4",
            Success = "#4CC07C",
            Warning = "#F0AC3C",
            Error = "#FF6A4D",
            Dark = "#02181C",

            Background = "#05171B",
            BackgroundGray = "#041316",
            Surface = "#0A2429",

            AppbarBackground = "rgba(5,23,27,0.72)",
            AppbarText = "#E4F1F0",

            DrawerBackground = "#04161A",
            DrawerText = "#B7D3D6",
            DrawerIcon = "#7FA8AD",

            TextPrimary = "#E4F1F0",
            TextSecondary = "#90AFB3",
            TextDisabled = "rgba(228,241,240,0.38)",

            ActionDefault = "#90AFB3",
            ActionDisabled = "rgba(228,241,240,0.26)",
            ActionDisabledBackground = "rgba(228,241,240,0.10)",

            Divider = "#16383E",
            DividerLight = "#123036",
            LinesDefault = "#16383E",
            LinesInputs = "#1E464D",
            TableLines = "#16383E",
            TableStriped = "rgba(53,181,188,0.05)",
            TableHover = "rgba(53,181,188,0.09)",

            Skeleton = "rgba(228,241,240,0.08)",
        },

        Typography = new Typography
        {
            // Instrument Sans untuk teks kerja: netral, sedikit sempit, enak di tabel padat.
            Default = new DefaultTypography
            {
                FontFamily = BodyStack,
                FontSize = ".9375rem",
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "normal"
            },
            // Archivo untuk judul: grotesque atletis, terasa seperti nomor punggung.
            H1 = new H1Typography { FontFamily = DisplayStack, FontSize = "3rem", FontWeight = "800", LineHeight = "1.04", LetterSpacing = "-.03em" },
            H2 = new H2Typography { FontFamily = DisplayStack, FontSize = "2.25rem", FontWeight = "800", LineHeight = "1.08", LetterSpacing = "-.03em" },
            H3 = new H3Typography { FontFamily = DisplayStack, FontSize = "1.875rem", FontWeight = "700", LineHeight = "1.12", LetterSpacing = "-.025em" },
            H4 = new H4Typography { FontFamily = DisplayStack, FontSize = "1.5rem", FontWeight = "700", LineHeight = "1.18", LetterSpacing = "-.02em" },
            H5 = new H5Typography { FontFamily = DisplayStack, FontSize = "1.1875rem", FontWeight = "700", LineHeight = "1.25", LetterSpacing = "-.015em" },
            H6 = new H6Typography { FontFamily = DisplayStack, FontSize = "1.0625rem", FontWeight = "700", LineHeight = "1.3", LetterSpacing = "-.01em" },

            Subtitle1 = new Subtitle1Typography { FontFamily = BodyStack, FontSize = ".9375rem", FontWeight = "600", LineHeight = "1.4" },
            Subtitle2 = new Subtitle2Typography { FontFamily = BodyStack, FontSize = ".875rem", FontWeight = "600", LineHeight = "1.4" },
            Body1 = new Body1Typography { FontFamily = BodyStack, FontSize = ".9375rem", FontWeight = "400", LineHeight = "1.6" },
            Body2 = new Body2Typography { FontFamily = BodyStack, FontSize = ".875rem", FontWeight = "400", LineHeight = "1.55" },
            Button = new ButtonTypography { FontFamily = BodyStack, FontSize = ".875rem", FontWeight = "600", LineHeight = "1.75", LetterSpacing = ".01em", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = BodyStack, FontSize = ".75rem", FontWeight = "400", LineHeight = "1.45" },
            // Overline = "eyebrow" bergaya papan skor.
            Overline = new OverlineTypography { FontFamily = MonoStack, FontSize = ".6875rem", FontWeight = "500", LineHeight = "1.6", LetterSpacing = ".14em", TextTransform = "uppercase" },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px",
            DrawerWidthLeft = "272px",
            AppbarHeight = "68px",
        },

        Shadows = BuildShadows(),
    };

    /// <summary>
    /// Bayangan bernuansa teal, bukan abu-abu netral — supaya kartu terasa
    /// mengambang di atas permukaan lapangan, bukan di atas kertas.
    /// </summary>
    private static Shadow BuildShadows()
    {
        var shadow = new Shadow();
        shadow.Elevation[1] = "0 1px 2px rgba(8,42,49,.06), 0 1px 1px rgba(8,42,49,.04)";
        shadow.Elevation[2] = "0 2px 6px rgba(8,42,49,.07), 0 1px 2px rgba(8,42,49,.05)";
        shadow.Elevation[3] = "0 4px 12px rgba(8,42,49,.08), 0 2px 4px rgba(8,42,49,.04)";
        shadow.Elevation[4] = "0 8px 20px rgba(8,42,49,.10), 0 2px 6px rgba(8,42,49,.05)";
        shadow.Elevation[6] = "0 12px 28px rgba(8,42,49,.12), 0 4px 10px rgba(8,42,49,.06)";
        shadow.Elevation[8] = "0 18px 40px rgba(8,42,49,.14), 0 6px 14px rgba(8,42,49,.07)";
        shadow.Elevation[12] = "0 26px 56px rgba(8,42,49,.16), 0 8px 20px rgba(8,42,49,.08)";
        return shadow;
    }
}

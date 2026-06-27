namespace YukaiLarkStateTransitionDiagram.Theme;

using System.Collections.Generic;

public static class KeyCapThemes
{
    public static IKeyCapTheme Current => YukaiLark;

    public static IKeyCapTheme YukaiLark { get; } = new YukaiLarkKeyCapTheme();
    public static IKeyCapTheme Office { get; } = new OfficeKeyCapTheme();
    public static IKeyCapTheme Gaming { get; } = new GamingKeyCapTheme();
    public static IKeyCapTheme Retro { get; } = new RetroKeyCapTheme();
    public static IKeyCapTheme CopyPaper { get; } = new CopyPaperKeyCapTheme();
    public static IKeyCapTheme Girly { get; } = new GirlyKeyCapTheme();
    public static IKeyCapTheme Edo { get; } = new EdoKeyCapTheme();
    public static IKeyCapTheme Hokusai { get; } = new HokusaiKeyCapTheme();
    public static IKeyCapTheme Monochrome { get; } = new MonochromeKeyCapTheme();
    public static IKeyCapTheme Mint { get; } = new MintKeyCapTheme();
    public static IKeyCapTheme Amber { get; } = new AmberKeyCapTheme();
    public static IKeyCapTheme Midnight { get; } = new MidnightKeyCapTheme();

    public static IReadOnlyList<IKeyCapTheme> AllThemes { get; } =
    [
        YukaiLark,
        Office,
        Gaming,
        Retro,
        CopyPaper,
        Girly,
        Edo,
        Hokusai,
        Monochrome,
        Mint,
        Amber,
        Midnight
    ];

    public static IReadOnlyList<IKeyCapTheme> ShortcutThemes { get; } =
    [
        YukaiLark,
        Gaming,
        Retro,
        CopyPaper,
        Girly,
        Hokusai,
        Monochrome,
        Mint,
        Amber,
        Midnight
    ];
}

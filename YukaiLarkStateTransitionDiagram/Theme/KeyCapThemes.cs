namespace SkylarkStateTransitionDiagram.Theme;

using System.Collections.Generic;

public static class KeyCapThemes
{
    public static IKeyCapTheme Current => Office;

    public static IKeyCapTheme Office { get; } = new OfficeKeyCapTheme();
    public static IKeyCapTheme Gaming { get; } = new GamingKeyCapTheme();
    public static IKeyCapTheme Retro { get; } = new RetroKeyCapTheme();
    public static IKeyCapTheme CopyPaper { get; } = new CopyPaperKeyCapTheme();
    public static IKeyCapTheme Girly { get; } = new GirlyKeyCapTheme();
    public static IKeyCapTheme Edo { get; } = new EdoKeyCapTheme();
    public static IKeyCapTheme Monochrome { get; } = new MonochromeKeyCapTheme();
    public static IKeyCapTheme Mint { get; } = new MintKeyCapTheme();
    public static IKeyCapTheme Amber { get; } = new AmberKeyCapTheme();
    public static IKeyCapTheme Midnight { get; } = new MidnightKeyCapTheme();

    public static IReadOnlyList<IKeyCapTheme> ShortcutThemes { get; } =
    [
        Office,
        Gaming,
        Retro,
        CopyPaper,
        Girly,
        Edo,
        Monochrome,
        Mint,
        Amber,
        Midnight
    ];
}

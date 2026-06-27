namespace YukaiLarkStateTransitionDiagram.Theme;

using System.Collections.Generic;

public static class KeyCapThemes
{
    public static IKeyCapTheme Current => YukaiLark;

    public static IKeyCapTheme YukaiLark { get; } = new YukaiLarkKeyCapTheme();
    public static IKeyCapTheme Kifuwarabe { get; } = new KifuwarabeKeyCapTheme();
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
    public static IKeyCapTheme PastelColors { get; } = new PastelColorsKeyCapTheme();
    public static IKeyCapTheme Beach { get; } = new BeachKeyCapTheme();
    public static IKeyCapTheme Roma { get; } = new RomaKeyCapTheme();
    public static IKeyCapTheme Tropical { get; } = new TropicalKeyCapTheme();
    public static IKeyCapTheme Forest { get; } = new ForestKeyCapTheme();
    public static IKeyCapTheme Desert { get; } = new DesertKeyCapTheme();
    public static IKeyCapTheme Snow { get; } = new SnowKeyCapTheme();
    public static IKeyCapTheme Rainy { get; } = new RainyKeyCapTheme();
    public static IKeyCapTheme NightSky { get; } = new NightSkyKeyCapTheme();
    public static IKeyCapTheme Grapefruit { get; } = new GrapefruitKeyCapTheme();
    public static IKeyCapTheme Lemon { get; } = new LemonKeyCapTheme();
    public static IKeyCapTheme Lime { get; } = new LimeKeyCapTheme();
    public static IKeyCapTheme Orange { get; } = new OrangeKeyCapTheme();
    public static IKeyCapTheme Peach { get; } = new PeachKeyCapTheme();
    public static IKeyCapTheme Strawberry { get; } = new StrawberryKeyCapTheme();
    public static IKeyCapTheme Marine { get; } = new MarineKeyCapTheme();
    public static IKeyCapTheme Sky { get; } = new SkyKeyCapTheme();
    public static IKeyCapTheme Sun { get; } = new SunKeyCapTheme();
    public static IKeyCapTheme Cloud { get; } = new CloudKeyCapTheme();
    public static IKeyCapTheme Moon { get; } = new MoonKeyCapTheme();
    public static IKeyCapTheme Star { get; } = new StarKeyCapTheme();
    public static IReadOnlyList<IKeyCapTheme> AllThemes { get; } =
    [
        YukaiLark,
        Kifuwarabe,
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
        Midnight,
        PastelColors,
        Beach,
        Roma,
        Tropical,
        Forest,
        Desert,
        Snow,
        Rainy,
        NightSky,
        Grapefruit,
        Lemon,
        Lime,
        Orange,
        Peach,
        Strawberry,
        Marine,
        Sky,
        Sun,
        Cloud,
        Moon,
        Star
    ];

    public static IReadOnlyList<IKeyCapTheme> ShortcutThemes { get; } =
    [
        YukaiLark,
        Kifuwarabe,
        Gaming,
        Retro,
        CopyPaper,
        Girly,
        Hokusai,
        Monochrome,
        Mint,
        Amber
    ];
}

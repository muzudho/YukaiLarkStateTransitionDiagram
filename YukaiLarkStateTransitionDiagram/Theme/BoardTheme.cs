namespace YukaiLarkStateTransitionDiagram.Theme;

using System;
using Microsoft.Xna.Framework;

public sealed record BoardTheme(
    Color BackgroundColor,
    Color GridColor,
    Color ExportBackdropColor,
    Color PhotoPaperColor,
    Color PhotoEdgeColor,
    Color PinColor,
    Color TransitionLineColor,
    Color TransitionLabelColor,
    Color SelectedTransitionLineColor,
    Color SelectedTransitionLabelColor,
    Color TransitionHandleColor,
    Color TransitionControlHandleColor,
    Color TransitionGuideColor)
{
    private bool IsLightBackground => GetLuminance(BackgroundColor) >= 0.58f;

    public Color HeaderBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 238)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.18f), 238);
    public Color HeaderBorderColor => WithAlpha(IsLightBackground ? PhotoEdgeColor : GridColor, 220);
    public Color HeaderTitleTextColor => IsLightBackground ? TransitionLabelColor : PhotoPaperColor;
    public Color HeaderStatusTextColor => IsLightBackground
        ? Blend(TransitionLabelColor, BackgroundColor, 0.18f)
        : TransitionLabelColor;

    public Color PanelBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 226)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.10f), 224);
    public Color PanelTopEdgeColor => WithAlpha(IsLightBackground ? PinColor : GridColor, 210);
    public Color PanelBottomEdgeColor => WithAlpha(Blend(BackgroundColor, Color.Black, 0.36f), 220);
    public Color PanelPrimaryTextColor => HeaderTitleTextColor;
    public Color PanelSecondaryTextColor => HeaderStatusTextColor;
    public Color PanelMutedTextColor => IsLightBackground
        ? Blend(TransitionLabelColor, BackgroundColor, 0.34f)
        : Blend(TransitionLabelColor, BackgroundColor, 0.18f);

    public Color BottomBarBackgroundColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 220)
        : WithAlpha(Blend(BackgroundColor, Color.Black, 0.14f), 218);

    public Color AssistantBubbleColor => IsLightBackground
        ? WithAlpha(PhotoPaperColor, 238)
        : WithAlpha(Blend(BackgroundColor, GridColor, 0.58f), 238);
    public Color AssistantBubbleBorderColor => WithAlpha(IsLightBackground ? PinColor : TransitionLineColor, 224);
    public Color AssistantCompletedBubbleBorderColor => WithAlpha(IsLightBackground ? PhotoEdgeColor : SelectedTransitionLineColor, 232);
    public Color AssistantTitleTextColor => IsLightBackground ? TransitionLabelColor : SelectedTransitionLabelColor;
    public Color AssistantBodyTextColor => IsLightBackground ? PanelSecondaryTextColor : Blend(SelectedTransitionLabelColor, TransitionLabelColor, 0.28f);
    public Color AssistantHintTextColor => IsLightBackground ? PanelMutedTextColor : Blend(SelectedTransitionLabelColor, BackgroundColor, 0.26f);
    public Color AssistantCutInShadowColor => WithAlpha(Blend(BackgroundColor, Color.Black, IsLightBackground ? 0.28f : 0.46f), 72);
    public Color AssistantCutInBandColor => WithAlpha(Blend(BackgroundColor, GridColor, IsLightBackground ? 0.42f : 0.28f), 166);
    public Color AssistantCutInFrameColor => WithAlpha(Blend(BackgroundColor, PhotoPaperColor, IsLightBackground ? 0.18f : 0.12f), 94);
    public Color AssistantCutInPrimaryTextColor => IsLightBackground ? TransitionLabelColor : SelectedTransitionLabelColor;
    public Color AssistantCutInSecondaryTextColor => IsLightBackground ? PanelSecondaryTextColor : Blend(SelectedTransitionLabelColor, TransitionLabelColor, 0.22f);

    private static float GetLuminance(Color color)
        => ((0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B)) / 255f;

    private static Color WithAlpha(Color color, byte alpha)
        => new(color.R, color.G, color.B, alpha);

    private static Color Blend(Color from, Color to, float amount)
    {
        var clamped = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)MathF.Round(MathHelper.Lerp(from.R, to.R, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.G, to.G, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.B, to.B, clamped)),
            (byte)MathF.Round(MathHelper.Lerp(from.A, to.A, clamped)));
    }
}
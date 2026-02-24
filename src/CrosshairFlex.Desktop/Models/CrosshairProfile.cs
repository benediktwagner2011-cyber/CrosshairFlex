namespace CrosshairFlex.Desktop.Models;

public enum CrosshairShape
{
    Plus = 0,
    Dot = 1,
    Circle = 2,
    Pump = 3,
    CustomPng = 4
}

public sealed class CrosshairProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public CrosshairShape Shape { get; set; } = CrosshairShape.Plus;
    public int Size { get; set; } = 42;
    public int Thickness { get; set; } = 4;
    public int Gap { get; set; } = 10;
    public int PumpCornerRounding { get; set; } = 12;
    public byte Red { get; set; } = 0;
    public byte Green { get; set; } = 255;
    public byte Blue { get; set; } = 0;
    public double Opacity { get; set; } = 0.95;
    public string CustomPngPath { get; set; } = string.Empty;
    public string ProfileHotkey { get; set; } = string.Empty;
    public List<string> ProfileHotkeys { get; set; } = [];

    public static CrosshairProfile CreateDefault() => new();
}

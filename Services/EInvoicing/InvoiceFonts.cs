using SkiaSharp;

namespace MailArchiver.Services.EInvoicing;

/// <summary>
/// Lädt die PDF-Schriften einmal pro Rendervorgang. Fehlt ein Zeichen in der
/// Sans-Schrift, wird zeichenweise auf Noto Sans CJK ausgewichen.
/// </summary>
public sealed class InvoiceFonts : IDisposable
{
    private readonly SKTypeface _sansRegular;
    private readonly SKTypeface _sansSemiBold;
    private readonly SKTypeface _monoRegular;
    private readonly SKTypeface _cjk;
    private readonly Dictionary<float, SKFont> _cjkBySize = new();

    public SKFont Title { get; }
    public SKFont H1 { get; }
    public SKFont H2 { get; }
    public SKFont Body { get; }
    public SKFont BodyBold { get; }
    public SKFont Small { get; }
    public SKFont Tiny { get; }
    public SKFont Mono { get; }
    public SKFont MonoSmall { get; }

    public InvoiceFonts(string fontDir)
    {
        _sansRegular = Load(fontDir, "plex-sans-400.ttf");
        _sansSemiBold = Load(fontDir, "plex-sans-600.ttf");
        _monoRegular = Load(fontDir, "plex-mono-400.ttf");
        _cjk = Load(fontDir, "NotoSansCJKsc-Regular.otf");

        Title = new SKFont(_sansSemiBold, 22);
        H1 = new SKFont(_sansSemiBold, 13);
        H2 = new SKFont(_sansSemiBold, 10);
        Body = new SKFont(_sansRegular, 9);
        BodyBold = new SKFont(_sansSemiBold, 9);
        Small = new SKFont(_sansRegular, 7.5f);
        Tiny = new SKFont(_sansRegular, 6.5f);
        Mono = new SKFont(_monoRegular, 8.5f);
        MonoSmall = new SKFont(_monoRegular, 7.5f);
    }

    /// <summary>Wählt für ein Zeichen die erste Schrift, die es darstellen kann.</summary>
    public SKFont Resolve(int codepoint, SKFont primary)
    {
        if (primary.GetGlyph(codepoint) != 0) return primary;

        var cjk = CjkFor(primary.Size);
        return cjk.GetGlyph(codepoint) != 0 ? cjk : primary;
    }

    private SKFont CjkFor(float size)
    {
        if (!_cjkBySize.TryGetValue(size, out var font))
        {
            font = new SKFont(_cjk, size);
            _cjkBySize[size] = font;
        }
        return font;
    }

    private static SKTypeface Load(string dir, string file)
    {
        string path = Path.Combine(dir, file);
        if (!File.Exists(path))
            throw new FileNotFoundException($"PDF font '{file}' missing in '{dir}'.", path);

        return SKTypeface.FromFile(path)
            ?? throw new InvalidOperationException($"PDF font '{path}' could not be loaded.");
    }

    public void Dispose()
    {
        Title.Dispose();
        H1.Dispose();
        H2.Dispose();
        Body.Dispose();
        BodyBold.Dispose();
        Small.Dispose();
        Tiny.Dispose();
        Mono.Dispose();
        MonoSmall.Dispose();
        foreach (var font in _cjkBySize.Values) font.Dispose();
        _sansRegular.Dispose();
        _sansSemiBold.Dispose();
        _monoRegular.Dispose();
        _cjk.Dispose();
    }
}

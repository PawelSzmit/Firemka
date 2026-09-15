using System.Globalization;
using Firemka.Application.AnnualClosing;
using Firemka.Application.Time;
using MigraDoc;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Firemka.Infrastructure.Pdf;

public sealed class AnnualReportPdfGenerator : IAnnualReportPdfGenerator
{
    public const string Version = "firemka-annual-pdf-r1";
    private static readonly object FontLock = new();
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    public byte[] Generate(AnnualReportPdfInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureFontResolver();
        PredefinedFontsAndChars.ErrorFontName = FiremkaFontResolver.FamilyName;

        var document = new Document { Info = { Title = $"Zestawienie roczne {input.TaxYear}" } };
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FiremkaFontResolver.FamilyName;
        normal.Font.Size = 9;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        var heading1 = document.Styles[StyleNames.Heading1]!;
        heading1.Font.Name = FiremkaFontResolver.FamilyName;
        heading1.Font.Size = 19;
        heading1.Font.Bold = true;
        heading1.Font.Color = Colors.DarkBlue;
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(10);
        var heading2 = document.Styles[StyleNames.Heading2]!;
        heading2.Font.Name = FiremkaFontResolver.FamilyName;
        heading2.Font.Size = 12;
        heading2.Font.Bold = true;
        heading2.Font.Color = Colors.DarkBlue;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(10);
        heading2.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Firemka - ");
        footer.AddText($"rok {input.TaxYear} - Strona ");
        footer.AddPageField();

        section.AddParagraph("Zestawienie danych firmowych", StyleNames.Heading1);
        var subtitle = section.AddParagraph();
        subtitle.AddFormattedText($"Rok {input.TaxYear} | wersja {input.VersionNumber}", TextFormat.Bold);
        subtitle.AddLineBreak();
        var generatedAtWarsaw = PolishBusinessTime.ToWarsawTime(input.GeneratedAtUtc);
        subtitle.AddText($"Utworzono: {generatedAtWarsaw:yyyy-MM-dd HH:mm} czasu polskiego");

        AddNotice(section, "To nie jest zeznanie PIT ani dokument gotowy do wysłania. " +
            "Zestawienie obejmuje wyłącznie dane działalności zapisane w Firemce. " +
            "Dane z etatu, wspólne rozliczenie, ulgi i inne źródła należy uzupełnić w zewnętrznej aplikacji PIT.");

        section.AddParagraph("Firma", StyleNames.Heading2);
        AddKeyValueTable(section,
            ("Nazwa", input.CompanyName),
            ("NIP", input.Nip),
            ("Adres", input.Address),
            ("Właściciel", input.OwnerName));

        section.AddParagraph("Dane do rozliczenia działalności", StyleNames.Heading2);
        AddMoneyTable(section,
            ("Przychody", input.Values.Revenue),
            ("Koszty przed uwzględnieniem spisu z natury", input.Values.CostsBeforeInventory),
            ("Spis z natury na początek roku", input.Values.OpeningInventory),
            ("Spis z natury na koniec roku", input.Values.ClosingInventory),
            ("Koszty po uwzględnieniu spisu z natury", input.Values.CostsAfterInventory),
            ("Składki społeczne odliczane od dochodu", input.Values.SocialContributions),
            ("Pozostałe korekty podstawy PIT", input.Values.PitAdjustments),
            (input.Values.PitIncome >= 0 ? "Dochód z działalności" : "Strata z działalności", Math.Abs(input.Values.PitIncome)),
            ("Zaliczki PIT należne", input.Values.PitAdvancesDue),
            ("Zaliczki PIT faktycznie wpłacone", input.Values.PitAdvancesPaid));

        section.AddParagraph("Roczne rozliczenie składki zdrowotnej - podsumowanie robocze", StyleNames.Heading2);
        AddMoneyTable(section,
            ("Dochód zdrowotny", input.Values.HealthIncome),
            ("Minimalna roczna podstawa", input.Values.AnnualHealthMinimumBase),
            ("Roczna podstawa przyjęta do wyliczenia", input.Values.AnnualHealthBasis),
            ("Składka roczna należna", input.Values.AnnualHealthContributionDue),
            ("Suma miesięcznych składek należnych", input.Values.HealthContributionsDueMonthly),
            (input.Values.HealthSettlementDifference >= 0 ? "Różnica roczna do dopłaty w ZUS" : "Roczna nadpłata do weryfikacji w ZUS", Math.Abs(input.Values.HealthSettlementDifference)),
            ("Składki faktycznie wpłacone", input.Values.HealthContributionsPaid),
            (input.Values.HealthPaymentDifference >= 0 ? "Różnica rocznej należności i wpłat" : "Wpłaty ponad roczną należność", Math.Abs(input.Values.HealthPaymentDifference)));
        AddNotice(section, "Roczne rozliczenie zdrowotnej wymaga ponownego sprawdzenia według zasad i profilu obowiązujących za ten rok.");

        section.AddParagraph("Miesiące ujęte w zamknięciu", StyleNames.Heading2);
        var months = section.AddTable();
        months.Borders.Width = 0.5;
        months.AddColumn(Unit.FromCentimeter(3.2));
        months.AddColumn(Unit.FromCentimeter(4));
        months.AddColumn(Unit.FromCentimeter(4));
        months.AddColumn(Unit.FromCentimeter(4));
        AddHeader(months, "Miesiąc", "Przychód", "Koszty", "Zaliczka PIT");
        foreach (var month in input.Months.OrderBy(item => item.Month))
        {
            var row = months.AddRow();
            row.Cells[0].AddParagraph(month.Month.ToString("MMMM yyyy", PolishCulture));
            row.Cells[1].AddParagraph(Money(month.Revenue));
            row.Cells[2].AddParagraph(Money(month.Costs));
            row.Cells[3].AddParagraph(Money(month.PitAdvanceDue));
        }

        section.AddParagraph("Potwierdzenie i źródła", StyleNames.Heading2);
        AddKeyValueTable(section,
            ("Data potwierdzenia", input.ConfirmedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Dowód niezależnego sprawdzenia", input.EvidenceReference),
            ("Źródła urzędowe", input.OfficialSources));

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AddNotice(Section section, string text)
    {
        var table = section.AddTable();
        table.AddColumn(Unit.FromCentimeter(15.2));
        var cell = table.AddRow().Cells[0];
        cell.Shading.Color = Color.FromRgb(238, 244, 250);
        cell.Format.LeftIndent = Unit.FromPoint(5);
        cell.Format.RightIndent = Unit.FromPoint(5);
        cell.AddParagraph(text);
        section.AddParagraph();
    }

    private static void AddKeyValueTable(Section section, params (string Label, string Value)[] rows)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.AddColumn(Unit.FromCentimeter(5.3));
        table.AddColumn(Unit.FromCentimeter(9.9));
        foreach (var (label, value) in rows)
        {
            var row = table.AddRow();
            row.Cells[0].Shading.Color = Color.FromRgb(242, 245, 248);
            row.Cells[0].AddParagraph(label).Format.Font.Bold = true;
            row.Cells[1].AddParagraph(value);
        }
    }

    private static void AddMoneyTable(Section section, params (string Label, decimal Value)[] rows)
        => AddKeyValueTable(section, rows.Select(item => (item.Label, Money(item.Value))).ToArray());

    private static void AddHeader(Table table, params string[] labels)
    {
        var row = table.AddRow();
        row.HeadingFormat = true;
        row.Shading.Color = Color.FromRgb(30, 70, 105);
        for (var index = 0; index < labels.Length; index++)
        {
            var paragraph = row.Cells[index].AddParagraph(labels[index]);
            paragraph.Format.Font.Bold = true;
            paragraph.Format.Font.Color = Colors.White;
        }
    }

    private static string Money(decimal value) => value.ToString("N2", PolishCulture) + " zł";

    private static void EnsureFontResolver()
    {
        lock (FontLock)
        {
            GlobalFontSettings.FontResolver ??= new FiremkaFontResolver();
        }
    }
}

internal sealed class FiremkaFontResolver : IFontResolver
{
    internal const string FamilyName = "Firemka Sans";
    private const string RegularFace = "firemka-regular";
    private const string BoldFace = "firemka-bold";

    public byte[]? GetFont(string faceName)
        => File.ReadAllBytes(ResolveFontPath(faceName == BoldFace));

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => familyName == FamilyName ? new FontResolverInfo(isBold ? BoldFace : RegularFace) : null;

    private static string ResolveFontPath(bool bold)
    {
        var candidates = bold
            ? new[] { "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/Library/Fonts/Arial Bold.ttf" }
            : new[] { "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf", "/System/Library/Fonts/Supplemental/Arial.ttf", "/Library/Fonts/Arial.ttf" };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("Brakuje czcionki Liberation Sans lub Arial potrzebnej do utworzenia PDF.");
    }
}

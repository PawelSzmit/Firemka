using Firemka.Domain.Documents;

namespace Firemka.Domain.Tests;

public sealed class IncomingDocumentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-10T12:00:00Z");

    [Fact]
    public void Missing_extracted_fields_block_confirmation_and_explain_what_to_fix()
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", Now);
        document.AttachSource(Guid.NewGuid(), new string('A', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, 0.12m, Now.AddMinutes(1));

        var exception = Assert.Throws<DocumentDataValidationException>(() =>
            document.ConfirmData(DocumentData.Empty, Now.AddMinutes(2)));

        Assert.Contains("numer faktury", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sprzedawc", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dat", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kwot", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SourceDocumentStatus.DataToReview, document.Status);
    }

    [Fact]
    public void Unreadable_scan_can_be_completed_manually_and_then_confirmed()
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", Now);
        document.AttachSource(Guid.NewGuid(), new string('B', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, null, Now.AddMinutes(1));

        document.ConfirmData(
            new DocumentData(
                "FV/9/2026",
                "Sztuczny Dostawca sp. z o.o.",
                "1234567890",
                new DateOnly(2026, 9, 9),
                123.45m,
                "PLN"),
            Now.AddMinutes(2));

        Assert.Equal(SourceDocumentStatus.RuleToDefine, document.Status);
        Assert.Equal("FV/9/2026", document.InvoiceNumber);
        Assert.Null(document.ExtractionConfidence);
        Assert.Equal(1, document.DataRevisionNumber);
    }

    [Fact]
    public void Marking_document_as_unrelated_requires_a_reason()
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", Now);
        document.AttachSource(Guid.NewGuid(), new string('C', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, null, Now.AddMinutes(1));

        Assert.Throws<ArgumentException>(() =>
            document.MarkUnrelated(" ", Now.AddMinutes(2)));

        document.MarkUnrelated("Prywatny zakup, omyłkowo dodany.", Now.AddMinutes(3));

        Assert.Equal(SourceDocumentStatus.UnrelatedToBusiness, document.Status);
        Assert.Equal("Prywatny zakup, omyłkowo dodany.", document.UnrelatedReason);
    }

    [Theory]
    [InlineData(SourceDocumentStatus.Booked)]
    [InlineData(SourceDocumentStatus.UnrelatedToBusiness)]
    public void Source_conflict_is_preserved_independently_of_a_terminal_status(
        SourceDocumentStatus terminalStatus)
    {
        var document = SourceDocument.CreateKsef(
            Guid.NewGuid(),
            "owner-1",
            "KSEF-TEST-1",
            Now,
            Now);
        document.AttachSource(Guid.NewGuid(), new string('D', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, 1m, Now.AddMinutes(1));
        if (terminalStatus == SourceDocumentStatus.UnrelatedToBusiness)
        {
            document.MarkUnrelated("Dokument testowo odrzucony.", Now.AddMinutes(2));
        }
        else
        {
            document.TransitionTo(terminalStatus, Now.AddMinutes(2));
        }

        document.MarkSourceConflict(Now.AddMinutes(3));

        Assert.Equal(terminalStatus, document.Status);
        Assert.True(document.HasSourceConflict);
        Assert.Equal(Now.AddMinutes(3), document.SourceConflictDetectedAtUtc);
    }

    [Fact]
    public void Source_conflict_can_be_resolved_without_erasing_its_history_or_terminal_status()
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", Now);
        document.MarkSourceConflict(new string('A', 64), Now.AddMinutes(1));

        document.ResolveSourceConflict(
            "Porównano zachowany plik z KSeF; dokument źródłowy jest właściwy.",
            Now.AddMinutes(2));

        Assert.True(document.HasSourceConflict);
        Assert.False(document.HasUnresolvedSourceConflict);
        Assert.Equal(Now.AddMinutes(1), document.SourceConflictDetectedAtUtc);
        Assert.Equal(Now.AddMinutes(2), document.SourceConflictResolvedAtUtc);
        Assert.Equal(
            "Porównano zachowany plik z KSeF; dokument źródłowy jest właściwy.",
            document.SourceConflictResolution);
        var firstConflict = Assert.Single(document.SourceConflicts);
        Assert.Equal(Now.AddMinutes(1), firstConflict.DetectedAtUtc);
        Assert.Equal(Now.AddMinutes(2), firstConflict.ResolvedAtUtc);
        Assert.Equal(
            "Porównano zachowany plik z KSeF; dokument źródłowy jest właściwy.",
            firstConflict.Resolution);

        document.MarkSourceConflict(new string('A', 64), Now.AddMinutes(3));

        Assert.False(document.HasUnresolvedSourceConflict);
        Assert.Equal(Now.AddMinutes(2), document.SourceConflictResolvedAtUtc);
        Assert.Single(document.SourceConflicts);

        document.MarkSourceConflict(new string('B', 64), Now.AddMinutes(4));

        Assert.True(document.HasUnresolvedSourceConflict);
        Assert.Equal(Now.AddMinutes(4), document.SourceConflictDetectedAtUtc);
        Assert.Null(document.SourceConflictResolvedAtUtc);
        Assert.Null(document.SourceConflictResolution);
        Assert.Collection(
            document.SourceConflicts.OrderBy(item => item.DetectedAtUtc),
            conflict => Assert.Equal(
                "Porównano zachowany plik z KSeF; dokument źródłowy jest właściwy.",
                conflict.Resolution),
            conflict => Assert.Null(conflict.Resolution));
    }

    [Fact]
    public void Source_conflict_resolution_requires_an_open_conflict_and_an_explanation()
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", Now);

        Assert.Throws<InvalidOperationException>(() =>
            document.ResolveSourceConflict("Wyjaśnienie.", Now.AddMinutes(1)));

        document.MarkSourceConflict(Now.AddMinutes(2));
        Assert.Throws<ArgumentException>(() =>
            document.ResolveSourceConflict(" ", Now.AddMinutes(3)));
    }
}

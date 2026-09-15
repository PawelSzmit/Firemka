using Firemka.Domain.Documents;
using Firemka.Domain.Invoices;

namespace Firemka.Domain.Tests;

public sealed class CoreDataModelTests
{
    [Fact]
    public void Source_document_rejects_a_transition_that_skips_required_review()
    {
        var document = SourceDocument.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-09-10T08:00:00Z"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => document.TransitionTo(
                SourceDocumentStatus.Booked,
                DateTimeOffset.Parse("2026-09-10T08:01:00Z")));

        Assert.Contains("Acquired", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Booked", exception.Message, StringComparison.Ordinal);
        Assert.Equal(SourceDocumentStatus.Acquired, document.Status);
    }

    [Fact]
    public void Source_document_accepts_the_review_path_to_booking()
    {
        var document = SourceDocument.Create(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DateTimeOffset.Parse("2026-09-10T08:00:00Z"));

        document.TransitionTo(
            SourceDocumentStatus.DataToReview,
            DateTimeOffset.Parse("2026-09-10T08:01:00Z"));
        document.TransitionTo(
            SourceDocumentStatus.Booked,
            DateTimeOffset.Parse("2026-09-10T08:02:00Z"));

        Assert.Equal(SourceDocumentStatus.Booked, document.Status);
        Assert.Equal(2, document.StateVersion);
    }

    [Fact]
    public void Invoice_correction_appends_a_version_without_changing_the_original()
    {
        var invoiceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var original = InvoiceVersion.CreateInitial(
            invoiceId,
            "{\"number\":\"FV/1\",\"amount\":100}",
            DateTimeOffset.Parse("2026-09-10T08:00:00Z"));

        var correction = original.CreateCorrection(
            "{\"number\":\"FV/1/K1\",\"amount\":90}",
            DateTimeOffset.Parse("2026-09-10T09:00:00Z"));

        Assert.Equal(1, original.VersionNumber);
        Assert.Equal("{\"number\":\"FV/1\",\"amount\":100}", original.SnapshotJson);
        Assert.Null(original.PreviousVersionId);
        Assert.Equal(2, correction.VersionNumber);
        Assert.Equal(original.Id, correction.PreviousVersionId);
        Assert.Equal(invoiceId, correction.InvoiceId);
        Assert.Equal("{\"number\":\"FV/1/K1\",\"amount\":90}", correction.SnapshotJson);
    }
}

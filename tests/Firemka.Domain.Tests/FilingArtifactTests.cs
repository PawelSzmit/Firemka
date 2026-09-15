using Firemka.Domain.Filings;

namespace Firemka.Domain.Tests;

public sealed class FilingArtifactTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T08:00:00Z");
    private static readonly string Fingerprint = new('a', 64);
    private static readonly string FileSha = new('b', 64);

    [Fact]
    public void Prepared_file_requires_separate_approval_before_manual_submission_and_one_final_outcome()
    {
        var artifact = FilingArtifact.Prepare(
            Guid.NewGuid(),
            "owner-1",
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            FilingArtifactKind.JpkV7M3,
            1,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Fingerprint,
            "JPK_V7M (3) 1-0E",
            "generator-r1",
            Guid.NewGuid(),
            FileSha,
            Now);

        Assert.Throws<InvalidOperationException>(() =>
            artifact.MarkSent("Klient JPK WEB — ręczna wysyłka", Now.AddMinutes(1)));

        artifact.Approve("Porównano podsumowanie z zamknięciem miesiąca.", Now.AddMinutes(2));
        artifact.MarkSent("Klient JPK WEB — ręczna wysyłka", Now.AddMinutes(3));
        artifact.RecordOutcome(
            FilingSubmissionOutcome.Accepted,
            Guid.NewGuid(),
            "UPO-TEST-1",
            Now.AddMinutes(4));

        Assert.Equal(FilingArtifactStatus.Accepted, artifact.Status);
        Assert.NotNull(artifact.ReceiptFileId);
        Assert.Throws<InvalidOperationException>(() =>
            artifact.RecordOutcome(
                FilingSubmissionOutcome.Rejected,
                Guid.NewGuid(),
                "drugi wynik",
                Now.AddMinutes(5)));
    }

    [Fact]
    public void Correction_is_a_new_unapproved_version_linked_to_the_previous_artifact()
    {
        var original = FilingArtifact.Prepare(
            Guid.NewGuid(),
            "owner-1",
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            FilingArtifactKind.ZusDraKedu227,
            1,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Fingerprint,
            "KEDU 2.27 / 5.7",
            "generator-r1",
            Guid.NewGuid(),
            FileSha,
            Now);
        original.Approve("Kontrola syntetyczna.", Now.AddMinutes(1));

        var correction = FilingArtifact.Prepare(
            original.CompanyId,
            original.OwnerUserId,
            Guid.NewGuid(),
            original.Period,
            original.Kind,
            2,
            original.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('c', 64),
            original.SchemaVersion,
            "generator-r1",
            Guid.NewGuid(),
            new string('d', 64),
            Now.AddMinutes(2));

        Assert.Equal(FilingArtifactStatus.ApprovalRequired, correction.Status);
        Assert.Equal(original.Id, correction.PreviousArtifactId);
        Assert.Equal(FilingArtifactStatus.Approved, original.Status);
        Assert.NotEqual(original.StoredFileId, correction.StoredFileId);
    }

    [Fact]
    public void Year_to_date_pkpir_cannot_be_marked_as_sent_before_the_year_is_closed()
    {
        var artifact = FilingArtifact.Prepare(
            Guid.NewGuid(),
            "owner-1",
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            FilingArtifactKind.JpkPkpir3,
            1,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Fingerprint,
            "JPK_PKPIR (3) 1-0",
            "generator-r1",
            Guid.NewGuid(),
            FileSha,
            Now);
        artifact.Approve("Techniczna kontrola narastającego pliku.", Now.AddMinutes(1));

        var error = Assert.Throws<InvalidOperationException>(() =>
            artifact.MarkSent("Niedozwolona wysyłka miesięczna", Now.AddMinutes(2)));

        Assert.Contains("zamknięciu roku", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

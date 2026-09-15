namespace Firemka.Domain.Documents;

public sealed class SourceDocument
{
    private readonly List<SourceDocumentConflict> _sourceConflicts = [];

    private static readonly IReadOnlyDictionary<SourceDocumentStatus, IReadOnlySet<SourceDocumentStatus>> AllowedTransitions =
        new Dictionary<SourceDocumentStatus, IReadOnlySet<SourceDocumentStatus>>
        {
            [SourceDocumentStatus.Acquired] = new HashSet<SourceDocumentStatus>
            {
                SourceDocumentStatus.DataToReview,
                SourceDocumentStatus.ErrorToResolve,
            },
            [SourceDocumentStatus.DataToReview] = new HashSet<SourceDocumentStatus>
            {
                SourceDocumentStatus.RuleToDefine,
                SourceDocumentStatus.Booked,
                SourceDocumentStatus.UnrelatedToBusiness,
                SourceDocumentStatus.ErrorToResolve,
            },
            [SourceDocumentStatus.RuleToDefine] = new HashSet<SourceDocumentStatus>
            {
                SourceDocumentStatus.Booked,
                SourceDocumentStatus.UnrelatedToBusiness,
                SourceDocumentStatus.ErrorToResolve,
            },
            [SourceDocumentStatus.ErrorToResolve] = new HashSet<SourceDocumentStatus>
            {
                SourceDocumentStatus.DataToReview,
            },
            [SourceDocumentStatus.Booked] = new HashSet<SourceDocumentStatus>(),
            [SourceDocumentStatus.UnrelatedToBusiness] = new HashSet<SourceDocumentStatus>(),
        };

    private SourceDocument()
    {
    }

    private SourceDocument(
        Guid id,
        string ownerUserId,
        SourceDocumentOrigin origin,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Origin = origin;
        Status = SourceDocumentStatus.Acquired;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public SourceDocumentOrigin Origin { get; private set; }

    public SourceDocumentStatus Status { get; private set; }

    public int StateVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid? SourceFileId { get; private set; }

    public string? SourceSha256 { get; private set; }

    public bool HasSourceConflict { get; private set; }

    public DateTimeOffset? SourceConflictDetectedAtUtc { get; private set; }

    public string? SourceConflictSha256 { get; private set; }

    public DateTimeOffset? SourceConflictResolvedAtUtc { get; private set; }

    public string? SourceConflictResolution { get; private set; }

    public bool HasUnresolvedSourceConflict =>
        HasSourceConflict && SourceConflictResolvedAtUtc is null;

    public IReadOnlyCollection<SourceDocumentConflict> SourceConflicts =>
        _sourceConflicts.AsReadOnly();

    public string? KsefNumber { get; private set; }

    public DateTimeOffset? KsefPermanentStorageDateUtc { get; private set; }

    public string? InvoiceNumber { get; private set; }

    public string? SellerName { get; private set; }

    public string? SellerTaxId { get; private set; }

    public string? SellerAddress { get; private set; }

    public DateOnly? IssueDate { get; private set; }

    public decimal? GrossAmount { get; private set; }

    public string? Currency { get; private set; }

    public decimal? ExtractionConfidence { get; private set; }

    public int DataRevisionNumber { get; private set; }

    public string? UnrelatedReason { get; private set; }

    public static SourceDocument Create(Guid id, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id dokumentu nie może być pusty.", nameof(id));
        }

        return new SourceDocument(id, "system", SourceDocumentOrigin.ManualUpload, createdAtUtc);
    }

    public static SourceDocument CreateManual(
        Guid id,
        string ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewDocument(id, ownerUserId);
        return new SourceDocument(id, ownerUserId, SourceDocumentOrigin.ManualUpload, createdAtUtc);
    }

    public static SourceDocument CreateKsef(
        Guid id,
        string ownerUserId,
        string ksefNumber,
        DateTimeOffset permanentStorageDateUtc,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewDocument(id, ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ksefNumber);
        return new SourceDocument(id, ownerUserId, SourceDocumentOrigin.Ksef, createdAtUtc)
        {
            KsefNumber = ksefNumber.Trim(),
            KsefPermanentStorageDateUtc = permanentStorageDateUtc,
        };
    }

    public void AttachSource(
        Guid storedFileId,
        string sha256,
        DateTimeOffset occurredAtUtc)
    {
        if (storedFileId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator pliku źródłowego nie może być pusty.", nameof(storedFileId));
        }

        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Skrót SHA-256 musi mieć 64 znaki szesnastkowe.", nameof(sha256));
        }

        if (SourceFileId is not null)
        {
            throw new InvalidOperationException("Dokument ma już zachowane źródło.");
        }

        SourceFileId = storedFileId;
        SourceSha256 = sha256.ToUpperInvariant();
        UpdatedAtUtc = occurredAtUtc;
    }

    public void ApplyExtraction(
        DocumentData data,
        decimal? confidence,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (SourceFileId is null)
        {
            throw new InvalidOperationException("Najpierw trzeba zachować plik źródłowy.");
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Pewność musi mieścić się od 0 do 1.");
        }

        ApplyData(data);
        ExtractionConfidence = confidence;
        TransitionTo(SourceDocumentStatus.DataToReview, occurredAtUtc);
    }

    public void ConfirmData(DocumentData data, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (Status != SourceDocumentStatus.DataToReview)
        {
            throw new InvalidOperationException("Potwierdzić można tylko dokument oczekujący na sprawdzenie danych.");
        }

        var errors = ValidateData(data);
        if (errors.Count > 0)
        {
            throw new DocumentDataValidationException(errors);
        }

        ApplyData(data);
        ExtractionConfidence = null;
        DataRevisionNumber++;
        TransitionTo(SourceDocumentStatus.RuleToDefine, occurredAtUtc);
    }

    public void MarkUnrelated(string reason, DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        UnrelatedReason = reason.Trim();
        TransitionTo(SourceDocumentStatus.UnrelatedToBusiness, occurredAtUtc);
    }

    public bool MarkSourceConflict(DateTimeOffset occurredAtUtc) =>
        MarkSourceConflict(null, null, occurredAtUtc);

    public bool IsKnownSourceConflict(string conflictingSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conflictingSha256);
        if (conflictingSha256.Length != 64 || !conflictingSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Skrót konfliktowego źródła musi mieć 64 znaki szesnastkowe.",
                nameof(conflictingSha256));
        }

        var normalizedSha256 = conflictingSha256.ToUpperInvariant();
        return string.Equals(SourceConflictSha256, normalizedSha256, StringComparison.Ordinal)
            || _sourceConflicts.Any(item =>
                string.Equals(item.ConflictingSha256, normalizedSha256, StringComparison.Ordinal));
    }

    public bool MarkSourceConflict(string? conflictingSha256, DateTimeOffset occurredAtUtc)
        => MarkSourceConflict(null, conflictingSha256, occurredAtUtc);

    public bool MarkSourceConflict(
        Guid? conflictingStoredFileId,
        string? conflictingSha256,
        DateTimeOffset occurredAtUtc)
    {
        if (conflictingStoredFileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Identyfikator konfliktowego pliku nie może być pusty.",
                nameof(conflictingStoredFileId));
        }

        if (conflictingSha256 is not null
            && (conflictingSha256.Length != 64 || !conflictingSha256.All(Uri.IsHexDigit)))
        {
            throw new ArgumentException(
                "Skrót konfliktowego źródła musi mieć 64 znaki szesnastkowe.",
                nameof(conflictingSha256));
        }

        var normalizedSha256 = conflictingSha256?.ToUpperInvariant();
        if (normalizedSha256 is not null && IsKnownSourceConflict(normalizedSha256))
        {
            return false;
        }

        _sourceConflicts.Add(new SourceDocumentConflict(
            Guid.NewGuid(),
            Id,
            conflictingStoredFileId,
            normalizedSha256,
            occurredAtUtc));
        HasSourceConflict = true;
        SourceConflictDetectedAtUtc = occurredAtUtc;
        SourceConflictSha256 = normalizedSha256;
        SourceConflictResolvedAtUtc = null;
        SourceConflictResolution = null;
        if (Status == SourceDocumentStatus.ErrorToResolve)
        {
            StateVersion++;
            UpdatedAtUtc = occurredAtUtc;
            return true;
        }

        if (Status is SourceDocumentStatus.Booked or SourceDocumentStatus.UnrelatedToBusiness)
        {
            StateVersion++;
            UpdatedAtUtc = occurredAtUtc;
            return true;
        }

        TransitionTo(SourceDocumentStatus.ErrorToResolve, occurredAtUtc);
        return true;
    }

    public void ResolveSourceConflict(string resolution, DateTimeOffset occurredAtUtc)
    {
        if (!HasUnresolvedSourceConflict)
        {
            throw new InvalidOperationException("Dokument nie ma nierozwiązanego konfliktu źródła.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
        var normalizedResolution = resolution.Trim();
        if (normalizedResolution.Length > 2_000)
        {
            throw new ArgumentException(
                "Wyjaśnienie konfliktu może mieć maksymalnie 2000 znaków.",
                nameof(resolution));
        }

        var openConflict = _sourceConflicts
            .Where(item => item.ResolvedAtUtc is null)
            .OrderBy(item => item.DetectedAtUtc)
            .ThenBy(item => item.Id)
            .LastOrDefault();
        if (openConflict is null)
        {
            openConflict = new SourceDocumentConflict(
                Guid.NewGuid(),
                Id,
                null,
                SourceConflictSha256,
                SourceConflictDetectedAtUtc ?? UpdatedAtUtc);
            _sourceConflicts.Add(openConflict);
        }

        openConflict.Resolve(normalizedResolution, occurredAtUtc);
        SourceConflictResolvedAtUtc = occurredAtUtc;
        SourceConflictResolution = normalizedResolution;
        if (Status == SourceDocumentStatus.ErrorToResolve)
        {
            TransitionTo(SourceDocumentStatus.DataToReview, occurredAtUtc);
            return;
        }

        StateVersion++;
        UpdatedAtUtc = occurredAtUtc;
    }

    public void MarkProcessingError(DateTimeOffset occurredAtUtc)
    {
        if (Status == SourceDocumentStatus.ErrorToResolve)
        {
            UpdatedAtUtc = occurredAtUtc;
            return;
        }

        TransitionTo(SourceDocumentStatus.ErrorToResolve, occurredAtUtc);
    }

    public void TransitionTo(SourceDocumentStatus nextStatus, DateTimeOffset occurredAtUtc)
    {
        if (!AllowedTransitions[Status].Contains(nextStatus))
        {
            throw new InvalidOperationException($"Transition from {Status} to {nextStatus} is not allowed.");
        }

        Status = nextStatus;
        StateVersion++;
        UpdatedAtUtc = occurredAtUtc;
    }

    private static void ValidateNewDocument(Guid id, string ownerUserId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id dokumentu nie może być pusty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
    }

    private static List<string> ValidateData(DocumentData data)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(data.InvoiceNumber))
        {
            errors.Add("numer faktury");
        }

        if (string.IsNullOrWhiteSpace(data.SellerName))
        {
            errors.Add("sprzedawcę");
        }

        if (data.IssueDate is null)
        {
            errors.Add("datę wystawienia");
        }

        if (data.GrossAmount is null or <= 0)
        {
            errors.Add("kwotę brutto większą od zera");
        }

        if (data.Currency is null
            || data.Currency.Trim().Length != 3
            || !data.Currency.All(char.IsLetter))
        {
            errors.Add("trzyliterową walutę");
        }

        return errors;
    }

    private void ApplyData(DocumentData data)
    {
        InvoiceNumber = data.InvoiceNumber?.Trim();
        SellerName = data.SellerName?.Trim();
        SellerTaxId = data.SellerTaxId?.Trim();
        SellerAddress = data.SellerAddress?.Trim();
        IssueDate = data.IssueDate;
        GrossAmount = data.GrossAmount;
        Currency = data.Currency?.Trim().ToUpperInvariant();
    }
}

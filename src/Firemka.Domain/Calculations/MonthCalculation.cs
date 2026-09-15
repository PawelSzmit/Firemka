namespace Firemka.Domain.Calculations;

public enum CalculationTrust
{
    TechnicalPreview = 1,
    ClosingEligible = 2,
}

public sealed record CalculationSourceLine(
    string Code,
    string Label,
    decimal Amount,
    string SourceReference);

public sealed record MonthCalculationInput(
    Guid CompanyId,
    string OwnerUserId,
    DateOnly Month,
    Guid RuleSetId,
    Guid DeclarationId,
    Guid? PreviousCalculationId,
    string InputFingerprint,
    decimal RevenueMonth,
    decimal CostsMonth,
    decimal SocialContributionsMonth,
    decimal PitBaseAdjustmentMonth,
    decimal HealthIncomeAdjustmentMonth,
    decimal RevenueYtdBefore,
    decimal CostsYtdBefore,
    decimal SocialContributionsYtdBefore,
    decimal PitAdjustmentsYtdBefore,
    decimal PriorPitAdvancesDue,
    decimal PriorVatCarryForward,
    decimal PreviousMonthHealthIncome,
    decimal OutputVatMonth,
    decimal InputVatMonth,
    CalculationTrust Trust,
    IReadOnlyCollection<CalculationSourceLine> SourceLines);

public sealed class MonthCalculation
{
    private readonly List<MonthCalculationLine> _lines = [];

    private MonthCalculation()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public DateOnly Month { get; private set; }

    public Guid RuleSetId { get; private set; }

    public Guid DeclarationId { get; private set; }

    public Guid? PreviousMonthCalculationId { get; private set; }

    public Guid? PreviousVersionId { get; private set; }

    public int VersionNumber { get; private set; }

    public string InputFingerprint { get; private set; } = string.Empty;

    public decimal RevenueMonth { get; private set; }

    public decimal CostsMonth { get; private set; }

    public decimal SocialContributionsMonth { get; private set; }

    public decimal PitBaseAdjustmentMonth { get; private set; }

    public decimal HealthIncomeAdjustmentMonth { get; private set; }

    public decimal RevenueYtd { get; private set; }

    public decimal CostsYtd { get; private set; }

    public decimal SocialContributionsYtd { get; private set; }

    public decimal PitAdjustmentsYtd { get; private set; }

    public decimal PitIncomeYtd { get; private set; }

    public decimal PitTaxBase { get; private set; }

    public decimal CumulativePitTax { get; private set; }

    public decimal PriorPitAdvancesDue { get; private set; }

    public decimal PitAdvanceDue { get; private set; }

    public bool CanDeferPitPayment { get; private set; }

    public decimal OutputVatMonth { get; private set; }

    public decimal InputVatMonth { get; private set; }

    public decimal PriorVatCarryForward { get; private set; }

    public decimal VatPayable { get; private set; }

    public decimal VatPayableRounded { get; private set; }

    public decimal VatCarryForward { get; private set; }

    public decimal CurrentHealthIncome { get; private set; }

    public decimal PreviousMonthHealthIncome { get; private set; }

    public decimal HealthMinimumBase { get; private set; }

    public decimal HealthBasis { get; private set; }

    public decimal HealthContribution { get; private set; }

    public CalculationTrust Trust { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<MonthCalculationLine> Lines => _lines.AsReadOnly();

    public static MonthCalculation Calculate(
        MonthCalculationInput input,
        CalculationRuleSet rules,
        int versionNumber,
        Guid? previousVersionId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        ValidateInput(input, rules, versionNumber, previousVersionId);

        var result = new MonthCalculation
        {
            Id = Guid.NewGuid(),
            CompanyId = input.CompanyId,
            OwnerUserId = input.OwnerUserId.Trim(),
            Month = input.Month,
            RuleSetId = input.RuleSetId,
            DeclarationId = input.DeclarationId,
            PreviousMonthCalculationId = input.PreviousCalculationId,
            PreviousVersionId = previousVersionId,
            VersionNumber = versionNumber,
            InputFingerprint = input.InputFingerprint.ToLowerInvariant(),
            RevenueMonth = RoundMoney(input.RevenueMonth),
            CostsMonth = RoundMoney(input.CostsMonth),
            SocialContributionsMonth = RoundMoney(input.SocialContributionsMonth),
            PitBaseAdjustmentMonth = RoundMoney(input.PitBaseAdjustmentMonth),
            HealthIncomeAdjustmentMonth = RoundMoney(input.HealthIncomeAdjustmentMonth),
            PriorPitAdvancesDue = RoundWhole(input.PriorPitAdvancesDue),
            OutputVatMonth = RoundMoney(input.OutputVatMonth),
            InputVatMonth = RoundMoney(input.InputVatMonth),
            PriorVatCarryForward = RoundMoney(input.PriorVatCarryForward),
            PreviousMonthHealthIncome = RoundMoney(input.PreviousMonthHealthIncome),
            Trust = input.Trust,
            CreatedAtUtc = nowUtc,
        };

        result.RevenueYtd = RoundMoney(input.RevenueYtdBefore + result.RevenueMonth);
        result.CostsYtd = RoundMoney(input.CostsYtdBefore + result.CostsMonth);
        result.SocialContributionsYtd = RoundMoney(
            input.SocialContributionsYtdBefore + result.SocialContributionsMonth);
        result.PitAdjustmentsYtd = RoundMoney(
            input.PitAdjustmentsYtdBefore + result.PitBaseAdjustmentMonth);
        result.PitIncomeYtd = RoundMoney(
            result.RevenueYtd
            - result.CostsYtd
            - result.SocialContributionsYtd
            + result.PitAdjustmentsYtd);
        result.PitTaxBase = RoundWhole(decimal.Max(0m, result.PitIncomeYtd));
        result.CumulativePitTax = CalculateScaleTax(result.PitTaxBase, rules.Values);
        result.PitAdvanceDue = RoundWhole(decimal.Max(
            0m,
            result.CumulativePitTax - result.PriorPitAdvancesDue));
        result.CanDeferPitPayment = result.PitAdvanceDue > 0m
            && result.PitAdvanceDue <= rules.PitPaymentOptionThreshold;

        var vatDifference = RoundMoney(
            result.OutputVatMonth - result.InputVatMonth - result.PriorVatCarryForward);
        result.VatPayable = decimal.Max(0m, vatDifference);
        result.VatPayableRounded = RoundWhole(result.VatPayable);
        result.VatCarryForward = decimal.Max(0m, -vatDifference);

        result.CurrentHealthIncome = RoundMoney(
            result.RevenueMonth
            - result.CostsMonth
            - result.SocialContributionsMonth
            + result.HealthIncomeAdjustmentMonth);
        result.HealthMinimumBase = rules.GetMinimumHealthBase(input.Month);
        result.HealthBasis = RoundMoney(decimal.Max(
            result.HealthMinimumBase,
            result.PreviousMonthHealthIncome));
        result.HealthContribution = RoundMoney(
            result.HealthBasis * rules.HealthRatePercent / 100m);

        result.AddLines(input.SourceLines);
        return result;
    }

    private void AddLines(IReadOnlyCollection<CalculationSourceLine> sources)
    {
        foreach (var source in sources)
        {
            AddLine(source.Code, source.Label, source.Amount, source.SourceReference, isFormula: false);
        }

        AddFormula("PIT_REVENUE_YTD", "Przychód narastająco", RevenueYtd);
        AddFormula("PIT_COSTS_YTD", "Koszty narastająco", CostsYtd);
        AddFormula("PIT_INCOME_YTD", "Dochód lub strata narastająco", PitIncomeYtd);
        AddFormula("PIT_TAX_BASE", "Podstawa PIT po zaokrągleniu", PitTaxBase);
        AddFormula("PIT_CUMULATIVE_TAX", "PIT narastająco", CumulativePitTax);
        AddFormula("PIT_ADVANCE_DUE", "Zaliczka PIT za miesiąc", PitAdvanceDue);
        AddFormula("VAT_PAYABLE", "VAT do zapłaty przed zaokrągleniem deklaracji", VatPayable);
        AddFormula("VAT_CARRY_FORWARD", "Nadwyżka VAT do przeniesienia", VatCarryForward);
        AddFormula("HEALTH_CURRENT_INCOME", "Dochód zdrowotny bieżącego miesiąca", CurrentHealthIncome);
        AddFormula("HEALTH_BASIS", "Podstawa składki zdrowotnej", HealthBasis);
        AddFormula("HEALTH_CONTRIBUTION", "Składka zdrowotna", HealthContribution);
    }

    private void AddFormula(string code, string label, decimal amount)
        => AddLine(code, label, amount, $"formula:{code.ToLowerInvariant()}", isFormula: true);

    private void AddLine(
        string code,
        string label,
        decimal amount,
        string sourceReference,
        bool isFormula)
    {
        _lines.Add(MonthCalculationLine.Create(
            Id,
            _lines.Count + 1,
            code,
            label,
            RoundMoney(amount),
            sourceReference,
            isFormula));
    }

    private static void ValidateInput(
        MonthCalculationInput input,
        CalculationRuleSet rules,
        int versionNumber,
        Guid? previousVersionId)
    {
        if (input.CompanyId == Guid.Empty || input.DeclarationId == Guid.Empty || input.RuleSetId == Guid.Empty)
        {
            throw new ArgumentException("Firma, deklaracja i zestaw zasad są wymagane.", nameof(input));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(input.OwnerUserId);
        if (input.Month.Day != 1)
        {
            throw new ArgumentException("Miesiąc kalkulacji musi zaczynać się pierwszego dnia.", nameof(input));
        }

        if (input.CompanyId != rules.CompanyId
            || input.RuleSetId != rules.Id
            || !string.Equals(input.OwnerUserId.Trim(), rules.OwnerUserId, StringComparison.Ordinal)
            || input.Month.Year != rules.TaxYear)
        {
            throw new InvalidOperationException("Kalkulacja i zestaw zasad dotyczą różnych danych właściciela.");
        }

        if (input.Trust == CalculationTrust.ClosingEligible
            && rules.Trust != CalculationRuleTrust.IndependentlyConfirmed)
        {
            throw new InvalidOperationException("Zamknięcie wymaga niezależnie potwierdzonego zestawu zasad.");
        }

        if (versionNumber <= 0
            || (versionNumber == 1 && previousVersionId is not null)
            || (versionNumber > 1 && previousVersionId is null))
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Wersja kalkulacji ma niespójnego poprzednika.");
        }

        ValidateFingerprint(input.InputFingerprint);
        ValidateNonNegative(input.RevenueMonth, nameof(input.RevenueMonth));
        ValidateNonNegative(input.CostsMonth, nameof(input.CostsMonth));
        ValidateNonNegative(input.SocialContributionsMonth, nameof(input.SocialContributionsMonth));
        ValidateNonNegative(input.RevenueYtdBefore, nameof(input.RevenueYtdBefore));
        ValidateNonNegative(input.CostsYtdBefore, nameof(input.CostsYtdBefore));
        ValidateNonNegative(input.SocialContributionsYtdBefore, nameof(input.SocialContributionsYtdBefore));
        ValidateNonNegative(input.PriorPitAdvancesDue, nameof(input.PriorPitAdvancesDue));
        ValidateNonNegative(input.PriorVatCarryForward, nameof(input.PriorVatCarryForward));
        ValidateNonNegative(input.OutputVatMonth, nameof(input.OutputVatMonth));
        ValidateNonNegative(input.InputVatMonth, nameof(input.InputVatMonth));

        ArgumentNullException.ThrowIfNull(input.SourceLines);
        foreach (var source in input.SourceLines)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Code);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Label);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceReference);
        }
    }

    private static decimal CalculateScaleTax(decimal taxBase, CalculationRuleValues rules)
    {
        decimal tax;
        if (taxBase <= rules.PitThreshold)
        {
            tax = taxBase * rules.PitLowerRatePercent / 100m - rules.PitReducingAmount;
        }
        else
        {
            tax = rules.PitThreshold * rules.PitLowerRatePercent / 100m
                - rules.PitReducingAmount
                + (taxBase - rules.PitThreshold) * rules.PitHigherRatePercent / 100m;
        }

        return RoundWhole(decimal.Max(0m, tax));
    }

    private static void ValidateFingerprint(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Odcisk wejścia musi być 64-znakowym SHA-256.", nameof(fingerprint));
        }
    }

    private static void ValidateNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Wartość nie może być ujemna.");
        }
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundWhole(decimal value)
        => decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed class MonthCalculationLine
{
    private MonthCalculationLine()
    {
    }

    private MonthCalculationLine(
        Guid calculationId,
        int sequence,
        string code,
        string label,
        decimal amount,
        string sourceReference,
        bool isFormula)
    {
        Id = Guid.NewGuid();
        MonthCalculationId = calculationId;
        Sequence = sequence;
        Code = code;
        Label = label;
        Amount = amount;
        SourceReference = sourceReference;
        IsFormula = isFormula;
    }

    public Guid Id { get; private set; }

    public Guid MonthCalculationId { get; private set; }

    public int Sequence { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string SourceReference { get; private set; } = string.Empty;

    public bool IsFormula { get; private set; }

    internal static MonthCalculationLine Create(
        Guid calculationId,
        int sequence,
        string code,
        string label,
        decimal amount,
        string sourceReference,
        bool isFormula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        if (calculationId == Guid.Empty || sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Linia wymaga kalkulacji i dodatniej kolejności.");
        }

        return new MonthCalculationLine(
            calculationId,
            sequence,
            code.Trim(),
            label.Trim(),
            amount,
            sourceReference.Trim(),
            isFormula);
    }
}

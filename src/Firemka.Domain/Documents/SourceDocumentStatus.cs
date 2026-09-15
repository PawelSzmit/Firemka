namespace Firemka.Domain.Documents;

public enum SourceDocumentStatus
{
    Acquired,
    DataToReview,
    RuleToDefine,
    Booked,
    UnrelatedToBusiness,
    ErrorToResolve,
}

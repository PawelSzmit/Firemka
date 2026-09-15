using Firemka.Application.AnnualClosing;
using Firemka.Domain.AnnualClosing;
using Firemka.Infrastructure.Persistence;

namespace Firemka.Infrastructure.AnnualClosing;

public sealed class AnnualArchiveRequestQueue(AppDbContext dbContext) : IAnnualArchiveRequestQueue
{
    public void Stage(Guid companyId, string ownerUserId, int taxYear, Guid annualClosingId, DateTimeOffset nowUtc)
    {
        if (dbContext.AnnualArchiveRequests.Local.Any(item => item.AnnualClosingId == annualClosingId)) return;
        dbContext.AnnualArchiveRequests.Add(
            AnnualArchiveRequest.Stage(companyId, ownerUserId, taxYear, annualClosingId, nowUtc));
    }
}

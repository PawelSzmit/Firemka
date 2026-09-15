using Firemka.Domain.Payments;

namespace Firemka.Domain.Tests;

public sealed class PolishDueDateTests
{
    [Fact]
    public void Tax_deadlines_move_from_weekends_and_public_holidays()
    {
        Assert.Equal(new DateOnly(2026, 10, 20), PolishDueDate.PitOrZusFor(new DateOnly(2026, 9, 1)));
        Assert.Equal(new DateOnly(2026, 10, 26), PolishDueDate.VatFor(new DateOnly(2026, 9, 1)));
        Assert.Equal(new DateOnly(2026, 12, 28), PolishDueDate.NextBusinessDay(new DateOnly(2026, 12, 25)));
        Assert.Equal(new DateOnly(2027, 3, 30), PolishDueDate.NextBusinessDay(new DateOnly(2027, 3, 29)));
    }
}

using Firemka.Application.ExternalServices;

namespace Firemka.Infrastructure.Ksef.Incoming;

public sealed class KsefIncomingOptions
{
    public const string SectionName = "Ksef:Incoming";

    public bool Enabled { get; set; }

    public KsefEnvironment Environment { get; set; } = KsefEnvironment.Test;

    public bool AllowProduction { get; set; }

    public string? AccessToken { get; set; }

    public Uri GetBaseUri() => Environment switch
    {
        KsefEnvironment.Test => new Uri("https://api-test.ksef.mf.gov.pl/v2/"),
        KsefEnvironment.Demo => new Uri("https://api-demo.ksef.mf.gov.pl/v2/"),
        KsefEnvironment.Production => new Uri("https://api.ksef.mf.gov.pl/v2/"),
        _ => throw new InvalidOperationException("Nieobsługiwane środowisko KSeF."),
    };

    public void ValidateForUse()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("Synchronizacja KSeF jest wyłączona.");
        }

        if (Environment == KsefEnvironment.Production && !AllowProduction)
        {
            throw new InvalidOperationException(
                "Środowisko produkcyjne KSeF wymaga osobnej, jawnej zgody w konfiguracji.");
        }
    }
}

using Firemka.Application.ExternalServices;

namespace Firemka.Infrastructure.Ksef.Outgoing;

public sealed class KsefOutgoingOptions
{
    public const string SectionName = "Ksef:Outgoing";

    public bool Enabled { get; set; }
    public bool AdapterConfigured { get; set; }
    public KsefEnvironment Environment { get; set; } = KsefEnvironment.Test;
    public bool AllowProduction { get; set; }
    public bool AllowAutomation { get; set; }

    public Uri GetBaseUri() => Environment switch
    {
        KsefEnvironment.Test => new Uri("https://api-test.ksef.mf.gov.pl/v2/"),
        KsefEnvironment.Demo => new Uri("https://api-demo.ksef.mf.gov.pl/v2/"),
        KsefEnvironment.Production => new Uri("https://api.ksef.mf.gov.pl/v2/"),
        _ => throw new InvalidOperationException("Nieznane środowisko KSeF."),
    };

    public void ValidateForUse()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("Wysyłka do KSeF jest wyłączona w konfiguracji serwera.");
        }

        if (!AdapterConfigured)
        {
            throw new InvalidOperationException(
                "Adapter wysyłki KSeF nie został jeszcze odebrany na środowisku testowym.");
        }

        if (Environment == KsefEnvironment.Production && !AllowProduction)
        {
            throw new InvalidOperationException(
                "Wysyłka do produkcyjnego KSeF wymaga osobnego zezwolenia operatora.");
        }
    }

    public void ValidateAutomationUse()
    {
        ValidateForUse();
        if (!AllowAutomation)
        {
            throw new InvalidOperationException(
                "Automatyczna wysyłka pozostaje zablokowana przez operatora aplikacji.");
        }
    }
}

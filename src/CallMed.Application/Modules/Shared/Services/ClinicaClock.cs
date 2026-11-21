namespace MKSANCrud.Services.Clinica;

public sealed class ClinicaClock : IClinicaClock
{
    private static readonly TimeZoneInfo TimeZone = ObterTimeZone();

    public DateTime Agora =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    public DateTime Hoje => Agora.Date;

    public DateTime ConverterUtc(DateTime utc)
    {
        var valorUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(valorUtc, TimeZone);
    }

    public DateTime ConverterParaUtc(DateTime local)
    {
        var valorLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(valorLocal, TimeZone);
    }

    private static TimeZoneInfo ObterTimeZone()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}

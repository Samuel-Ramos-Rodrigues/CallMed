namespace MKSANCrud.Services.Clinica;

public interface IClinicaClock
{
    DateTime Agora { get; }
    DateTime Hoje { get; }
    DateTime ConverterUtc(DateTime utc);
    DateTime ConverterParaUtc(DateTime local);
}

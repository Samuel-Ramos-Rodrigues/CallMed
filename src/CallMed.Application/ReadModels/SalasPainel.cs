namespace MKSANCrud.Models;
public sealed record ConsultaSalaLinha(Consulta Consulta,Jornada? Jornada,string Estado,bool PacientePresente,bool ProfissionalPresente,IReadOnlyList<SessaoTele> Historico,string Conexao);
public sealed record SalaFisicaLinha(SalaAtendimento Sala,string Estado,IReadOnlyList<ConsultaSalaLinha> Reservas,IReadOnlyList<BloqueioSala> Bloqueios);
public sealed record SalasPainel(DateTime Data,string Aba,bool Equipe,IReadOnlyList<SalaFisicaLinha> Salas,IReadOnlyList<ConsultaSalaLinha> Online,IReadOnlyList<ConsultaSalaLinha> SemSala,IReadOnlyList<ProfissionalSaude> Profissionais,int? ProfissionalId,bool LimiteAtingido);

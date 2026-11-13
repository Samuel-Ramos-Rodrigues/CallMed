using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
internal static class AmploChecks {
 public static async Task RunAsync(Action<bool,string> check) {
  var medico=new ProfissionalSaude{AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow};
  check(medico.HabilitadoClinicamente&&!medico.EhPsicologia&&!medico.HabilitadoPsicologia&&medico.RegistroExibicao=="CRM 12345/SE","Profissional de Medicina tem registro próprio e não assume permissões clínicas de Psicologia");
  medico.RegistroVerificadoEm=null;check(!medico.HabilitadoClinicamente,"Registro médico preenchido exige verificação administrativa");
  var legado=new ProfissionalSaude{Crm="CRM-LEGADO",RegistroVerificadoEm=DateTime.UtcNow};check(!legado.HabilitadoClinicamente&&legado.Crp==null,"CRM legado não vira CRP nem habilitação automática sem classificar profissão");
  var psi=new ProfissionalSaude{Crp="12/12345",RegistroVerificadoEm=DateTime.UtcNow};check(psi.HabilitadoPsicologia,"Cadastro antigo de Psicologia continua usando CRP verificado");
  var outro=new ProfissionalSaude{AreaProfissional="Outra profissão de saúde",RegistroConselho="CRO",RegistroNumero="7890",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow};check(outro.HabilitadoClinicamente&&!outro.EhPsicologia,"Outra profissão usa conselho, número e UF próprios");
  foreach(var (area,c,n,u) in new[]{("Medicina","CRM","12345","XX"),("Medicina","CRM","CRM-LEGADO","SE"),("Medicina","CRM","0","SE"),("Psicologia","CRP","12345","SE"),("Outra profissão de saúde","CRP","12/12345","SE"),("Outra profissão de saúde","CRM","12345","SE"),("Inexistente","CRM","12345","SE")})check(!RegistroProfissional.Normalizar(area,c,n,u,out _,out _,out _),"Conselho reservado, regional ausente ou categoria inválida é recusado");
  var consulta=new Consulta{Medico=medico};check(ConsentimentoAtendimento.Versao(consulta)=="atendimento-video-v1"&&!ConsentimentoAtendimento.Texto(consulta).Contains("psicólogo"),"Consulta geral usa consentimento de vídeo correspondente à modalidade geral");
  consulta.Medico=psi;check(ConsentimentoAtendimento.Versao(consulta)=="psicologia-video-v1","Psicologia mantém sua versão específica de consentimento");
  ClaimsPrincipal User(string role)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"teste"),new Claim(ClaimTypes.Role,role)],"test"));
  foreach(var role in new[]{"Admin","Funcionario","Medico","Psicologo","Paciente"}){var user=User(role);var principais=NavegacaoCallMed.Principais(user);check(principais.Length<=4,"Navegação principal por perfil não excede quatro atalhos");var itens=NavegacaoCallMed.Itens(user);check(itens.Select(x=>(x.Controller,x.Action)).Distinct().Count()==itens.Length,"Áreas do perfil não duplicam o mesmo destino");if(role!="Admin")check(!itens.Any(x=>x.Controller is "Acessos" or "Configuracoes" or "Auditoria"),"Menu de outro perfil não oferece ações administrativas");}
  await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("amplo-"+Guid.NewGuid()).Options);
  medico.Nome="Fictício";medico.EspecialidadeId=1;db.Profissionais.Add(medico);await db.SaveChangesAsync();check(await db.Medicos.CountAsync()==1&&await db.Psicologos.CountAsync()==1,"Aliases legados apontam para a mesma entidade profissional, sem duplicar cadastros");
 }
}

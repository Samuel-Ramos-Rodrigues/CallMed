#!/usr/bin/env python3
"""Verificação estática do pacote da demanda. Não substitui testes de integração .NET."""
from pathlib import Path
p=Path(__file__).resolve().parents[1]
checks={
    'Menu por perfil': ('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs','"Teleconsultas"'),
    'Painel e agenda reais': ('src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml','@Model.ConsultasHoje'),
    'Prioridade da recepção': ('src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml','@Model.SolicitacoesAtrasadas'),
    'Fila: oferta válida': ('src/CallMed.Web/Controllers/Atendimento/ListaEsperaController.cs','x.ExpiraEm > agora'),
    'Fila: revogação de reserva': ('src/CallMed.Web/Controllers/Atendimento/ListaEsperaController.cs','oferta.Estado = "Revogada"'),
    'Pré-atendimento paciente': ('src/CallMed.Web/Views/Home/Index.cshtml','Pré-atendimento e check-in'),
    'Estilos em CSS único': ('src/CallMed.Web/wwwroot/css/callmed.css','.cm-demand-focus'),
    'Lembrete: links seguros': ('src/CallMed.Application/Jobs/LembreteConsultaBackgroundService.cs','ConsultaLinkService'),
    'Encaixe: reserva automática': ('src/CallMed.Application/Modules/Agenda/Services/AgendamentoService.cs','_encaixes.ReservarAsync'),
    'Teleconsultas com rota correta': ('src/CallMed.Web/Views/Shared/_NavegacaoPrincipal.cshtml','asp-route-aba='),
    'Painel teleconsultas': ('src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml','asp-route-aba="online"'),
    'Teleconsultas profissionais': ('src/CallMed.Web/Views/MedicoPainel/Index.cshtml','Abrir minhas teleconsultas'),
    'Preparação paciente': ('src/CallMed.Web/Views/Home/Index.cshtml','Preparar consulta online'),
    'E-mail: configuração real': ('src/CallMed.Web/Views/Integracoes/Index.cshtml','envioAutomatico'),
}
for desc,(rel,needle) in checks.items():
    f=p/rel
    assert f.is_file() and needle in f.read_text(encoding='utf-8'),f'{desc}: ausente ou incorreto'
    print('OK',desc)
for rel in ['mobile/android/callmed-upload.jks','mobile/android/keystore.properties']:
    assert not (p/rel).exists(),f'Material de assinatura privado incluído: {rel}'
assert (p/'mobile/android/keystore.properties.example').exists()
print('OK chaves de assinatura excluídas; apenas exemplo')

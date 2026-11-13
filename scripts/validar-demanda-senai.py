#!/usr/bin/env python3
"""Verificação estática do pacote da demanda. Não substitui testes de integração .NET."""
from pathlib import Path
p=Path(__file__).resolve().parents[1]
checks={
    'Menu por perfil': ('src/CallMed.Application/Modules/Contas/Services/NavegacaoCallMed.cs','"Teleconsultas"'),
    'Painel e agenda reais': ('CallMedCrud/Views/FuncionarioPainel/Index.cshtml','@Model.ConsultasHoje'),
    'Prioridade da recepção': ('CallMedCrud/Views/FuncionarioPainel/Index.cshtml','@Model.SolicitacoesAtrasadas'),
    'Fila: oferta válida': ('CallMedCrud/Controllers/Atendimento/ListaEsperaController.cs','x.ExpiraEm > agora'),
    'Fila: revogação de reserva': ('CallMedCrud/Controllers/Atendimento/ListaEsperaController.cs','oferta.Estado = "Revogada"'),
    'Pré-atendimento paciente': ('CallMedCrud/Views/Home/Index.cshtml','Pré-atendimento e check-in'),
    'Estilos em CSS único': ('CallMedCrud/wwwroot/css/callmed.css','.cm-demand-focus'),
    'Lembrete: links seguros': ('src/CallMed.Application/Jobs/LembreteConsultaBackgroundService.cs','ConsultaLinkService'),
    'Encaixe: reserva automática': ('src/CallMed.Application/Modules/Agenda/Services/AgendamentoService.cs','_encaixes.ReservarAsync'),
    'Teleconsultas com rota correta': ('CallMedCrud/Views/Shared/_NavegacaoPrincipal.cshtml','asp-route-aba='),
    'Painel teleconsultas': ('CallMedCrud/Views/FuncionarioPainel/Index.cshtml','asp-route-aba="online"'),
    'Teleconsultas profissionais': ('CallMedCrud/Views/MedicoPainel/Index.cshtml','Abrir minhas teleconsultas'),
    'Preparação paciente': ('CallMedCrud/Views/Home/Index.cshtml','Preparar consulta online'),
    'E-mail: configuração real': ('CallMedCrud/Views/Integracoes/Index.cshtml','envioAutomatico'),
}
for desc,(rel,needle) in checks.items():
    f=p/rel
    assert f.is_file() and needle in f.read_text(encoding='utf-8'),f'{desc}: ausente ou incorreto'
    print('OK',desc)
for rel in ['CallMedAndroid/callmed-upload.jks','CallMedAndroid/keystore.properties']:
    assert not (p/rel).exists(),f'Material de assinatura privado incluído: {rel}'
assert (p/'CallMedAndroid/keystore.properties.example').exists()
print('OK chaves de assinatura excluídas; apenas exemplo')

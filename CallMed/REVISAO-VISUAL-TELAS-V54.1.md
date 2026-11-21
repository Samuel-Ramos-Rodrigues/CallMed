# Revisão visual das telas — CallMed NEXT V54.1

Revisão de 16/11/2026 sobre CallMed-NEXT-V54.1-APK-ESTILIZADO.zip, incluindo as imagens de login, download Android e avaliação do assistente enviadas pelo usuário.

## Correções

| Local | Causa encontrada | Ajuste |
| --- | --- | --- |
| Download Android | A classe do link recebeu fundo branco, padding e layout de cartão, mantendo texto branco | Estilos do cartão separados do botão, ação principal legível, espaçamento e dados do aplicativo organizados |
| Login e acesso | Link de criar conta transformado em grade; seta na linha seguinte | Texto e ícone alinhados na mesma ação |
| Login | Foco automático podia rolar a página ao abrir; margens de campos somadas ao espaçamento da grade | Remoção do autofocus e dos espaços duplicados; logo visível no topo |
| Campo de senha | Seletor genérico dos inputs sobrepunha a reserva para o botão Mostrar | Reserva de espaço com seletor específico, sem sobrepor o texto digitado |
| Central de Atendimento | Filtros tinham a mesma divisão de largura apesar de Todas possuir texto e contador | Texto mantido em uma linha, espaço mínimo para ícones e rolagem interna quando necessária |
| Agenda no celular | Título Profissionais quebrava no meio da palavra | Coluna ajustada ao tamanho da fonte, com título em uma linha |
| Navegação inferior | Espaçamento dos itens cortava o último controle em 390 px | Larguras e padding ajustados; textos ampliados continuam com rolagem interna acessível |
| Avaliação do assistente | Formulário encolhia dentro de uma coluna flex, com botões cortados e layout de rolagem inadequado | Formulário mantém altura natural; área de mensagens rola; cabeçalho, status e envio mantêm sua posição |
| Notas da avaliação | Duas camadas de bordas e padding reduziam a área útil | Opções compactas com seleção e foco visíveis; ações alcançáveis em telas pequenas |
| Atendimento encerrado | Campo de mensagem não esclarecia a ação seguinte | Status e placeholder explicam que enviar uma mensagem inicia outro atendimento, preservando o fluxo atual do servidor |

## Verificação

- Compilação .NET/Razor: aprovada, 0 erros e 0 avisos.
- 48 páginas por perfil carregadas: todas responderam HTTP 200.
- Temas claro e escuro: 96 verificações automatizadas de acessibilidade/contraste, sem ocorrências detectadas no conjunto avaliado.
- Celular a 390 px e fonte a 100%/200%: sem rolagem horizontal da página nos 96 cenários avaliados. A agenda e a navegação com texto ampliado podem rolar dentro de seus próprios componentes.
- Conferência dirigida: 36 combinações de seis páginas, larguras 1440/390/320 e dois temas, com verificações de alinhamento, contraste do botão de download, topo do login, campo de senha e controles da navegação.
- Assistente aberto e encerrado: oito cenários em 1440 × 900, 390 × 844, 320 × 640 e 390 × 480, nos dois temas. Botões da avaliação alcançáveis, oito envios de nota simulados e oito novas mensagens após encerrar.
- Sintaxe JavaScript do assistente e auditoria estrutural: aprovadas.
- As verificações utilizaram dados fictícios e ambiente local. Os endpoints do assistente foram simulados para testar seus estados sem enviar mensagens nem avaliações reais. Não houve acesso ao banco de produção nem publicação no Render.

## Aplicação

Use este projeto atualizado no próximo deploy do site. O cache estático foi atualizado para v110 para renovar os estilos. As correções desta revisão pertencem à interface web. O pacote foi posteriormente atualizado com o APK de teste 54.1.2; consulte CORRECAO-VIDEO-APK-V54.1.2.md.

## Páginas percorridas

| Tela/cenário | Perfil de teste | Rota |
| --- | --- | --- |
| publico | anon | `/` |
| login | anon | `/acesso` |
| cadastro | anon | `/Identity/Account/Register` |
| recuperacao | anon | `/Identity/Account/ForgotPassword` |
| privacidade-publica | anon | `/privacidade` |
| termos | anon | `/termos-de-uso` |
| android | anon | `/aplicativo` |
| recepcao | f | `/FuncionarioPainel` |
| admin-inicio | admin | `/FuncionarioPainel` |
| agenda | f | `/Agenda` |
| pacientes | f | `/Paciente` |
| novo-paciente | f | `/Paciente/Create` |
| detalhes-paciente | f | `/Paciente/Details/1` |
| editar-paciente | f | `/Paciente/Edit/1` |
| profissionais | admin | `/Medico` |
| novo-profissional | admin | `/Medico/Create` |
| equipe | admin | `/Funcionario` |
| especialidades | admin | `/Especialidade` |
| disponibilidades | admin | `/Disponibilidades` |
| convenios | admin | `/Convenios` |
| solicitacoes | f | `/Solicitacoes` |
| consultas-equipe | f | `/Consulta` |
| espera | f | `/ListaEspera` |
| exames | f | `/HistoricoExames/Pendentes` |
| relatorios | admin | `/Relatorios` |
| configuracoes | admin | `/Configuracoes` |
| integracoes | admin | `/Integracoes` |
| resultados | admin | `/Operacao` |
| piloto | admin | `/Piloto` |
| medicao | admin | `/Piloto/Medicao` |
| privacidade-gestao | admin | `/dados-e-privacidade/gestao` |
| areas | admin | `/Organizacao` |
| acessos | admin | `/Acessos` |
| mensagens | f | `/Atendimento?id=100` |
| jornada | f | `/Jornada` |
| jornada-detalhe | patient | `/Jornada/Consulta/1` |
| pep | admin | `/Psicologia/Pep` |
| paciente-inicio | patient | `/Home` |
| minhas-consultas | patient | `/Consulta` |
| agendamento | patient | `/Consulta/Create` |
| conta | patient | `/MinhaConta` |
| privacidade-paciente | patient | `/dados-e-privacidade` |
| profissional | doctor | `/MedicoPainel` |
| salas | f | `/Salas` |
| salas-online | f | `/Salas?aba=online` |
| agendamento-equipe | f | `/Consulta/Create` |
| novo-atendimento | f | `/Atendimento/Nova` |
| nova-solicitacao | f | `/Solicitacoes/Create` |

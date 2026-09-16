# Validação da V22

Data: 15/09/2026. Base: ZIP V21.6.5 enviado pelo usuário.

## Executado

- Restauração das dependências e build Release com SDK .NET 8.0.425: sucesso, zero erros e zero avisos após implementar os módulos.
- Verificação final dos fontes C# e Razor com Roslyn do mesmo SDK, referências restauradas e gerador Razor: sucesso. O compilador foi invocado diretamente na verificação final porque a leitura de informações de processos pelo MSBuild passou a falhar no ambiente de execução. Não foi necessário alterar o código da aplicação para isso.
- Onze verificações de código executadas contra a compilação final: e-mail opcional, e-mail inválido, antecedência configurada, documentação por convênio, orientação por especialidade, atendimento particular, Evolution conectada, consulta GET sem envio, instância desconectada, chave inválida e JSON inválido.
- Oito cenários JavaScript com Happy DOM e Web Crypto real do Node: cifragem, recuperação/senha inválida, resposta perdida/reenvio, acesso negado, aba desatualizada, falta de espaço, bloqueio e seleção de fallback no service worker.
- Sintaxe JavaScript do formulário e service worker conferida.
- YAML do Docker Compose analisado: quatro serviços definidos, sem erro de estrutura YAML.

Os testes da fila usam armazenamento, locks e respostas HTTP simulados; não comprovam a transação/índice em um PostgreSQL real. O teste de service worker também é simulado. Saídas dos testes estão nos arquivos `TESTES-CODIGO-V22.txt` e `TESTES-FILA-V22.txt`.

## Não executado neste ambiente

- Navegador Chromium real e inspeção visual: navegador indisponível e download falhou. O teste Playwright foi incluído para execução local.
- Aplicação do patch V22 e agendamentos simultâneos em banco de homologação. Nenhum banco externo foi acessado ou modificado.
- Inicialização dos containers Docker e execução do script PowerShell na máquina do usuário.
- Conexão real com Evolution, QR Code, envio/recebimento de WhatsApp, SMS ou SMTP. Nenhuma mensagem foi enviada.
- Integração com o HIS específico da empresa, cujo contrato/acesso não foi disponibilizado.
- Testes com pacientes e funcionários, orçamento da infraestrutura e comprovação de redução de faltas/tempo.

O roteiro em `../ROTEIRO-HOMOLOGACAO-V22.md` detalha os critérios para esses itens. A entrega contém a implementação e ferramentas de verificação; não declara homologação operacional concluída.

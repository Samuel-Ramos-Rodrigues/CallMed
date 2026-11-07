# Refatoração por módulos — 06/11/2026

Escopo escolhido: refatorar o projeto existente, sem reescrita. Mesmas variáveis de ambiente e mesmo banco.

## O que mudou

- **Aceite de vaga em um único lugar.** Novo serviço `Services/Clinica/ListaEsperaAceiteService.cs`. Antes, a regra estava escrita duas vezes: no controller da lista de espera (portal do paciente) e no link de um toque. Agora os dois chamam o mesmo serviço, então as regras não podem divergir.
- **Tarefa de telefone sem duplicação.** O serviço de lembretes usava o método auxiliar `CriarTarefaTelefoneAsync` só em um dos caminhos e repetia a lógica no outro. Agora todos os casos passam pelo método auxiliar, que também preenche o e-mail de contato.

## O que não mudou

- Nenhum comportamento visível para o paciente ou para a equipe. As mensagens de sucesso e de erro do aceite são as mesmas.
- Estrutura de pastas e namespaces permanecem os mesmos. Mover arquivos sem compilador aumenta o risco de quebrar referências.
- Nenhuma variável de ambiente nova.

## Verificação

- Não compilado (ambiente sem .NET SDK). Foram conferidos o equilíbrio de chaves e parênteses, a unicidade dos métodos alterados e o registro da injeção de dependência.
- Testar antes de publicar: aceitar vaga pelo portal; aceitar vaga pelo link; link de vaga já ocupada; lembrete de paciente com canal Telefone (deve criar uma única tarefa, sem duplicar na próxima execução).

## Próximo passo sugerido

Reorganizar as pastas por módulo (Agenda, Atendimento, Lista de espera, Pré-consulta, Telemedicina) em uma etapa separada, quando houver compilador disponível. Fazer a mudança de pastas e de namespaces junta com a compilação evita quebras silenciosas.

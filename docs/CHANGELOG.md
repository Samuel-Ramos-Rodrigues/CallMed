# CallMed V21.6 — organização estrutural

- Controllers separados por domínio/área sem alterar rotas.
- DTOs, Options e ViewModels separados das entidades e controllers.
- EF Core organizado em `Data/Configurations`.
- `Program.cs` simplificado com extensions e startup initializer.
- Interfaces/Workers/Initializers organizados em subpastas.
- PWA/TWA atualizado para 21.6.0.

# Changelog

## 21.3.0 — limpeza estrutural

- consolidou a cascata autenticada em `site.css`;
- consolidou estilos públicos em `public.css`;
- consolidou estilos de autenticação em `identity.css`;
- removeu arquivos CSS antigos por número de versão do runtime;
- simplificou a lista de assets do Service Worker;
- ampliou a proteção de rotas dinâmicas contra cache indevido;
- removeu documentação histórica e scripts de limpeza já obsoletos;
- atualizou README, TWA e nomes dos artifacts do GitHub Actions;
- preservou integralmente controllers, models, services, migrations, integrações e regras de negócio da V21.2.

## V21.5 — acabamento mobile/PWA
- corrigida a topbar do paciente em celulares estreitos;
- removido o fundo branco da marca mobile e a possibilidade de logo duplicada;
- marca compacta centralizada no mobile;
- tema sai da topbar abaixo de 420 px e continua disponível em Minha Conta;
- Modo fácil continua no menu de acessibilidade `Aa`, evitando controle duplicado na Home;
- atalhos do paciente agora ficam em grade 2x2 até 480 px;
- card sem próxima consulta foi reorganizado para evitar aperto do CTA;
- barra inferior ficou mais leve e compacta, preservando área de toque;
- Service Worker atualizado para `callmed-static-v21-5-mobile-polish`;
- TWA atualizado para 21.5.0.

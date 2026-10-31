# CallMed — painéis por perfil (31/10/2026)

Base: CallMed-CSS-Classico-Completo-Layout-Recuperado.zip, preservando integralmente a estrutura do repositório.

## Telas revisadas
- Identidade/Login.cshtml: apresentação do acesso único e informações sobre os quatro perfis.
- Home/Index.cshtml: painel do paciente com atalhos, resumo, próximos passos e modo simples preservado.
- MedicoPainel/Index.cshtml: indicadores e atalhos internos para agenda e bloqueios. Ações médicas permanecem restritas ao próprio médico.
- FuncionarioPainel/Index.cshtml: painéis de atendimento e administração com informações diferenciadas conforme a role.
- CSS único callmed.css: complementos específicos dos painéis e mobile.

## Verificação
- O pacote de origem inteiro foi preservado, incluindo Dockerfile, CallMed.sln, testes, scripts, docs, PlayStore e CallMedTWA.
- Corrigida referência inválida RankingItemViewModel.Valor para Total.
- Corrigidos atalhos do médico que apontavam para controllers exclusivos de Funcionario/Admin.
- Preservado modo fácil da área do paciente (.patient-standard).
- Integrações e controllers não foram alterados.

## Limitações
- Este ambiente não disponibiliza o comando dotnet. Compilação ASP.NET Core 8 e testes com login real/banco de dados requerem homologação.

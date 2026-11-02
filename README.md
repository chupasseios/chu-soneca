# Chu Soneca

Aplicativo Windows integrado ao vPilot para acompanhar controladores VATSIM na rota do voo.

## Instalação

Baixe `Chu-Soneca-Setup-X.Y.Z.exe` em **Releases**. Feche o vPilot antes de instalar ou atualizar. O instalador não encerra o cliente em execução.

O CID vem automaticamente da sessão do vPilot. Em Preferências, informe seu usuário/Pilot ID do SimBrief, importe o plano e inicie o monitoramento. Reimporte quando mudar de plano. O SimBrief é consultado somente por ação do usuário.

- Centros: interseção com o trecho restante da rota; alerta dentro da área ou em aproximação a 15 NM.
- APP/DEP: raio de 65 NM do aeroporto associado.
- Alerta de ATC: resposta em 2 minutos; descida sustentada após cruzeiro: 5 minutos.
- Ações protegidas por CID/sessão, telemetria válida e bloqueio no solo.
- Sem rota validada, ou com desvio acima de 30 NM, ações de ATC ficam bloqueadas.
- Histórico UTC e preferências de abertura com vPilot e encerramento completo.

O app não substitui a presença e a responsabilidade do piloto na rede. Consulte [packaging/CHANGELOG.txt](packaging/CHANGELOG.txt) para regras e limites da versão.

## Build local (Windows)

Requer Git, .NET SDK 10, .NET Framework 4.8 e 7-Zip. Não precisa instalar ou abrir o vPilot para compilar.

```powershell
./scripts/Build.ps1
```

O script baixa Node 24.12.0 e o instalador oficial vPilot 3.12.1, valida hashes fixos em `scripts/dependencies.json` e extrai duas DLLs somente para compilação. Não executa o instalador do vPilot, não copia configurações pessoais e não redistribui essas DLLs. Os fontes do Radar são vendorizados e não dependem de outro checkout.

Executa testes do motor, estado, identidade e instalador. Saída: `artifacts/` com instalador, SHA256 e relatório. Os testes não enviam comandos ao vPilot ou simulador.

## GitHub Actions e releases

Push em `main`, pull requests e execução manual compilam/testam e guardam os artefatos. Tags `vX.Y.Z` publicam uma release somente após todos os testes.

1. Altere `VERSION` e `packaging/CHANGELOG.txt`.
2. Faça commit e push para `main`; aguarde o build passar.
3. Crie e envie uma tag que corresponda exatamente ao arquivo VERSION:

```powershell
git tag -a v0.7.1 -m "Chu Soneca 0.7.1"
git push origin v0.7.1
```

O workflow usa `GITHUB_TOKEN`; nenhum token pessoal deve ser salvo no código. A permissão de escrita é restrita ao job de release. O instalador não possui atualização automática via GitHub ainda; as releases são baixadas manualmente.

## Licenças e dados

Uso não comercial. O motor inclui fontes VATSIM Radar sob CC BY-NC 4.0, preservados em `engine/vendor`, e dependências com licenças próprias em `packaging/licenses`. Consulte [ATRIBUICOES.md](ATRIBUICOES.md). A imagem do mascote fornecida pelo usuário não recebe licença adicional por estar neste repositório.

Não versionar logs, planos SimBrief, preferências, credenciais, DLLs do SDK, dependências instaladas ou binários de build. `.gitignore` exclui esses arquivos.

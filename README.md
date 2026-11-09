# Chu Soneca

Aplicativo Windows integrado ao vPilot e ao xPilot 4 para acompanhar controladores VATSIM na rota do voo.

## Instalação

Baixe `Chu-Soneca-Setup-X.Y.Z.exe` em **Releases**. Feche o vPilot e o xPilot antes de instalar ou atualizar. O instalador não encerra o cliente em execução.

O CID vem automaticamente da sessão do cliente escolhido. Inicie o monitoramento após conectar. O plano enviado à VATSIM fornece origem, destino, nível e rota. Trechos diretos usam aeroportos VATSpy, coordenadas ICAO (incluindo abreviações do Atlântico Norte), fixos do Aviation Weather Center e auxílios do OurAirports com posição identificada sem ambiguidade. Para aerovias, SID, STAR ou pontos sem coordenadas, salve opcionalmente seu usuário ou Pilot ID do SimBrief em Preferências. O app consulta o último navlog e confere indicativo, aeroportos e rota com o plano da rede. Uma falha na consulta mantém o monitoramento e o nível do plano VATSIM. As ações por cobertura aguardam a geometria validada.

- Centros: interseção com o trecho restante da rota; alerta dentro da área ou em aproximação a 15 NM.
- APP/DEP: raio de 65 NM do aeroporto associado.
- Alerta de ATC: resposta em 2 minutos; descida sustentada após cruzeiro: 5 minutos.
- Ações protegidas por CID/sessão, telemetria válida e bloqueio no solo.
- Sem rota validada, ou com desvio acima de 30 NM, ações de ATC ficam bloqueadas.
- Histórico UTC e preferências de abertura com o cliente e encerramento completo.

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

O workflow usa `GITHUB_TOKEN`; nenhum token pessoal deve ser salvo no código. A permissão de escrita é restrita ao job de release. O plugin consulta a última release estável ao abrir e a cada 6 horas. Quando há uma versão mais nova, mostra um aviso na tela com acesso às releases para baixar o instalador. Falhas de rede não interrompem o monitoramento. A instalação continua manual, com o vPilot fechado.

## Licenças e dados

Uso não comercial. O motor inclui fontes VATSIM Radar sob CC BY-NC 4.0, preservados em `engine/vendor`, e dependências com licenças próprias em `packaging/licenses`. Consulte [ATRIBUICOES.md](ATRIBUICOES.md). A imagem do mascote fornecida pelo usuário não recebe licença adicional por estar neste repositório.

Não versionar logs, planos SimBrief, preferências, credenciais, DLLs do SDK, dependências instaladas ou binários de build. `.gitignore` exclui esses arquivos.

A margem de saída no plugin é fixa em 15 NM. Em Preferências, marque **Não reconectar automaticamente, mesmo após sair 15 NM** para manter a reconexão manual. Desmarcado, o retorno após desconexão ATC feita pelo app exige duas consultas confirmando área livre além dos 15 NM. A opção é salva e não interrompe o monitoramento. Desconexões por descida ou Supervisor continuam sem retorno automático.

## xPilot / X-Plane (Windows)

A partir da 0.8.0, o instalador oferece **vPilot · Microsoft Flight Simulator** e **xPilot 4 · X-Plane**. O adaptador xPilot requer o SDK oficial **0.2.0.0**, conferido ao carregar. xPilot 2/3 e versões de SDK diferentes não são suportados. Esta distribuição é para Windows 64 bits, com .NET Framework 4.8 para a janela; não é um pacote para macOS/Linux.

1. Instale e abra o xPilot 4 uma vez; feche os dois clientes antes de instalar.
2. Selecione xPilot no instalador. A pasta de dados é `%LOCALAPPDATA%/org.vatsim.xpilot` (o comando `.plugins` do xPilot abre a subpasta Plugins).
3. Abra X-Plane e xPilot no **mesmo computador**, com o X-Plane recebendo UDP na porta padrão **49000**. A telemetria usa assinaturas RREF somente de leitura, duas vezes por segundo. Dados ausentes, incompletos, inválidos ou antigos bloqueiam ações; a assinatura é refeita automaticamente. Portas personalizadas e simulador remoto não são suportados nesta etapa.
4. Conecte-se pelo xPilot: o CID e callsign vêm do evento do cliente, sem digitação. Confira o plano VATSIM e inicie o monitoramento na janela Chu Soneca.
5. Abertura manual pelo atalho **Chu Soneca - xPilot**, com xPilot aberto. A opção de abrir com o cliente e a de fechar completamente estão disponíveis também nesta integração.

O instalador pode ser executado novamente para adicionar a outra integração, mantendo a anterior. Ao atualizar qualquer opção, atualiza também a outra integração já registrada. A desinstalação remove ambas as integrações deste produto e preserva outros plugins. Preferências e históricos xPilot ficam em `%LOCALAPPDATA%/FlightWatch/xPilot`, separados dos do vPilot.

O adaptador só pede reconexão após uma desconexão ATC iniciada por ele, com a opção de retorno habilitada, além da margem fixa de 15 NM e duas consultas livres. Descida, Supervisor, desconexão manual, solo, sessão divergente, perda do canal e telemetria inválida não autorizam retorno. O comando utiliza as credenciais atuais do cliente; uma nova conexão sempre identifica novamente o CID pelo evento oficial. Nenhuma senha é lida ou armazenada pelo Chu Soneca.

Validação automatizada: broker simulado do SDK xPilot, CID/sessão, chamadas RequestDisconnect/RequestConnect, replay/prazos, solo/pausa/aceleração, conversão e validade da telemetria UDP, regressão vPilot, instalação isolada e conjunta, rollback e remoção. **Não houve validação com xPilot e X-Plane reais em voo.** Faça primeiro um teste controlado; compatibilidade em operação real ainda precisa ser confirmada.

Referências: [SDK xPilot](https://github.com/xpilot-project/plugin-sdk), [guia de plugins](https://xpilot.app/docs/plugins), [datarefs do X-Plane](https://developer.x-plane.com/datarefs/). O SDK MIT está fixado no commit indicado em `xpilot/sdk/UPSTREAM.txt`; seu DLL não é redistribuído, pois o cliente fornece a implementação.

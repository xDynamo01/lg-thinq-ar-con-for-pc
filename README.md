# LG ThinQ Ar Con for PC — Clima

Controle de ar-condicionado LG ThinQ para Windows, com painel completo e controle compacto na bandeja do sistema. Projeto independente, sem vínculo com a LG.

## Instalação

1. Baixe `Clima-Windows-x64.zip` em [Releases](https://github.com/xDynamo01/lg-thinq-ar-con-for-pc/releases).
2. Extraia para uma pasta permanente e abra `ThinQDesktop.exe`. O pacote inclui o runtime necessário para Windows x64.
3. Abra as configurações (engrenagem). Crie **seu próprio token pessoal** em [LG ThinQ Connect](https://connect-pat.lgthinq.com/tokens), usando a conta à qual seu ar está vinculado. Autorize as permissões necessárias para consultar dispositivos, perfis e estados e enviar comandos.
4. Informe o token e o país da conta, conecte e selecione o ar-condicionado. As opções atuais são BR, US, PT e KR.

O executável não possui assinatura digital. O Windows pode exibir um aviso ao abri-lo.

Para iniciar com o Windows, use a opção no menu do ícone da bandeja. Fechar o painel mantém o aplicativo na bandeja; use **Sair** para encerrá-lo. Se mover a pasta, desative e ative novamente a inicialização automática.

## Recursos

- Temperatura, modo, velocidade de ventilação, oscilação e iluminação, conforme as capacidades informadas pelo aparelho.
- Controle de temperatura em passos de 1 °C; o controle arredonda a apresentação, enquanto o painel pode mostrar a leitura recebida.
- Timer e Sleep separados, seletores gráficos e animações para ventilar, resfriar e secar.
- Painel compacto, atualização periódica de estado e recuperação de conexão.
- Credenciais protegidas localmente pelo Windows.

## Compatibilidade e limites

Projeto experimental: as funções variam por modelo, região e perfil disponibilizado pela LG. A comunicação depende da internet e da nuvem ThinQ; a resposta não é instantânea. O estado é consultado a cada 30 segundos enquanto um painel está aberto. Comandos de escrita não são repetidos automaticamente após uma falha.

AI Care e Jet Mode permanecem indisponíveis nesta integração. Posições fixas das aletas e funções exclusivas do controle infravermelho também não estão implementadas. Os timers dependem do suporte do aparelho. O aplicativo não modifica firmware nem controla diretamente o compressor.

## Privacidade

Este repositório e o pacote de distribuição não incluem token pessoal, conta configurada, identificadores de aparelhos, APKs ou diagnósticos pessoais. Cada pessoa conecta a própria conta.

O token é salvo em `%LOCALAPPDATA%\ThinQDesktop\account.bin`, protegido com DPAPI para o usuário atual do Windows. A opção de esquecer a conta remove a credencial salva. Não publique essa pasta ou diagnósticos sem revisar os dados.

O cliente contém uma chave **pública de aplicação** usada pelo SDK público da LG; ela não é um token pessoal e não concede acesso à conta de ninguém. Consulte [as atribuições](THIRD-PARTY-NOTICES.md).

## Compilar e verificar

Requer Windows e SDK .NET 9:

```powershell
dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
$result = Start-Process .\dist\ThinQDesktop.exe -ArgumentList '--self-test' -PassThru -Wait
Get-Content .\dist\tests.txt
```

`--self-test` executa verificações locais sem comandar um aparelho. `--preview-animations` abre uma prévia visual offline. A execução normal conecta somente após configuração local de uma conta. Não execute opções de diagnóstico de controle real sem entender os comandos enviados.

Contribuições são bem-vindas. Ao relatar problemas, inclua versão do Windows, versão do aplicativo e passos para reprodução, sem tokens, identificadores pessoais ou APKs.

## Licença

Código original sob MIT. Referências ao SDK ThinQ Connect sob Apache 2.0, conforme os arquivos de licença e atribuição. LG e ThinQ são marcas de seus respectivos titulares.

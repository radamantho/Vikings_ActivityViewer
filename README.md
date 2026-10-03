# Vikings_ActivityViewer

Programa para Windows que lê o banco SQLite gerado pelo mod **Vikings_ActivityLog** e permite investigar a atividade dos jogadores.

*English version: [README.en.md](README.en.md)*

## Instalação

Execute `Setup_Vikings_ActivityViewer_<versão>.exe`. Não é preciso instalar o .NET nem ter permissão de administrador.

## Idioma

O programa está em **Português** e **English**. Na primeira abertura ele usa o idioma escolhido no instalador (ou o idioma do Windows). Para trocar, abra **Configurações** (engrenagem no canto superior direito); o programa reinicia já traduzido.

## Primeiro uso

1. Na barra lateral, clique no lápis ao lado de **Servidor** e crie um perfil:
   - Protocolo: FTP, FTPS ou SFTP.
   - Host, porta, usuário e senha da hospedagem.
   - Pasta remota: a pasta do `-savedir` do servidor seguida de `/Vikings_ActivityLog` (ex.: `/SAVE/Vikings_ActivityLog`).
   - Use **Testar conexão** para conferir; ele lista os mundos (`.db`) encontrados.
2. Escolha o servidor, clique no botão de atualizar ao lado de **Mundo**, escolha o mundo e clique em **Baixar**.
3. O programa baixa uma cópia consistente do banco (até 3 tentativas se o servidor estiver gravando), abre a cópia e preenche todas as abas.

Para um servidor no mesmo PC, use o ícone de pasta ao lado de **Baixar** e escolha o `.db` direto na pasta do servidor.

A senha fica salva criptografada pelo Windows e só pode ser lida pelo seu usuário neste PC.
Cada admin cria os próprios perfis no próprio PC.

## Filtros

- **Jogador** e **período** (De / Até, formato `dd/MM/aaaa` ou `dd/MM/aaaa HH:mm`) valem para todas as abas.
- "Até" com apenas a data inclui o dia inteiro. Campo vazio = sem limite.
- **Analisar** recarrega jogadores e sugestões e refaz a busca de todas as abas.

## Abas

- **Dano**: dano causado (Damage) e sofrido (Damaged), com tipos de dano e vida restante.
- **Itens**: itens pegos, largados, movidos, craftados, equipados e consumidos, com origem e destino.
- **Ações/seg**: jogadores com mais ações por segundo do que o limite (macro/cheat).
- **Velocidade**: deslocamentos impossíveis entre posições do próprio jogador (ignora teleporte, morte e respawn).
- **Interações**: interações com objetos, uso de itens e textos escritos.

Em todas as abas: ordenar clicando no cabeçalho, **Copiar ID** (SteamID da linha) e **Exportar** (CSV para Excel ou TXT, sempre com o resultado completo). Em português o CSV usa `;` e vírgula decimal.

## Arquivos do programa

- Perfis: `%AppData%\Vikings_ActivityViewer\profiles.json`
- Idioma: `%AppData%\Vikings_ActivityViewer\settings.json`
- Cópias baixadas: `%LocalAppData%\Vikings_ActivityViewer\cache\`
- Registro de erros: `%LocalAppData%\Vikings_ActivityViewer\logs\`

## Configurações

Abra pela engrenagem no canto superior direito:

- **Idioma**: Português ou English (o programa reinicia).
- **Pasta das cópias baixadas**: escolha onde salvar os bancos baixados. Ao trocar, as cópias existentes são movidas para a pasta nova.
- **Limpar cópia atual / Limpar todas**: apaga as cópias baixadas neste PC. Os bancos nos servidores nunca são alterados.

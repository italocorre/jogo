# 🌊 Mar Aberto

Jogo de mundo aberto no mar, em primeira pessoa, para jogar com os amigos (até 20 jogadores).
Você começa numa ilha com uma picareta, minera, compra um barco, melhora ele, explora o mar
atrás de ilhas novas e entra em guerra com os barcos dos amigos.

O design completo está em [`docs/GDD.md`](docs/GDD.md). Sons e fontes de uso livre: veja [`CREDITOS.md`](CREDITOS.md).

## ⬇️ Baixar e jogar

1. Abra a aba **Releases** do repositório (lado direito da página do GitHub).
2. Baixe o **MarAberto-Windows.zip** da versão mais recente.
3. Extraia a pasta inteira e abra **MarAberto.exe**.

> O Windows pode mostrar "O Windows protegeu o computador". Clique em **Mais informações → Executar assim mesmo**
> (acontece com qualquer jogo que não é assinado digitalmente).

### Controles

| Tecla | Ação |
|---|---|
| W A S D | Andar / pilotar o barco |
| Shift | Correr / nadar mais rápido |
| Espaço | Pular / subir na água (e subir no píer ou no barco) |
| Ctrl ou C | Mergulhar |
| Botão esquerdo | Minerar (com a picareta na mão) |
| E | Pegar item, falar com o mercador, pilotar/sair do barco, subir no barco pela água |
| R | Soltar / recolher a âncora (no barco) |
| 1 a 6 / rodinha | Trocar item da mão |
| Tab ou I | Abrir a mochila (arraste para organizar) |
| Q | Largar o item da mão |
| Esc | Menu (sensibilidade, volume, sair) |
| H | Mostrar/esconder ajuda |

### Como jogar
1. Minere rochas (pedra, carvão, cobre) — os pedaços caem no chão: olhe e aperte **E** para pegar.
2. Pegue conchas e cocos na praia.
3. Venda tudo para o **Seu Joca**, na barraca listrada perto do píer.
4. Junte **150 moedas** e compre o **bote a motor** — ele aparece no fim do píer.
5. Pilote até a **boia vermelha** (aparece um marcador na tela): ali tem um **recife**.
6. Aperte **R** para soltar a âncora, pule na água e mergulhe: corais, minérios ricos e **ostras com pérolas** (25 moedas cada!).
7. Melhore o barco no mercador: **motor a vapor nível 2 e 3** e **corda longa da âncora** (recifes mais fundos).
8. Há outros recifes escondidos mais longe da ilha. Explore!

## 👥 Jogar com amigos (até 20)

1. **Quem hospeda** abre o jogo, digita o nome e clica **HOSPEDAR PARTIDA** (escolha quantos jogadores).
   O mundo fica salvo no computador de quem hospeda e continua de onde parou na próxima vez.
2. **Os amigos** abrem o jogo, digitam o IP de quem hospeda e clicam **ENTRAR**.
3. Como conectar:
   - **Mesma casa (Wi-Fi):** use o IP que aparece na tela de quem hospeda (ex.: `192.168.0.10`).
   - **Pela internet, jeito fácil:** todos instalam **Radmin VPN** ou **ZeroTier** (grátis), entram na mesma rede
     e usam o IP da rede virtual de quem hospeda.
   - **Pela internet, sem VPN:** quem hospeda libera a **porta 7777 (TCP)** no roteador e passa o IP público.
   - Na primeira vez, o Windows pergunta se pode liberar o jogo no firewall: clique **Permitir**.
4. Todos precisam da **mesma versão** do jogo (mesmo build).

O jogo salva sozinho a cada minuto e ao fechar. Cada jogador tem sua mochila, receitas e cama guardadas pelo nome.

## ⚙️ Configurar o build automático (uma vez só)

Toda vez que o código muda na branch `main`, o GitHub compila o jogo sozinho e publica o `.exe` em **Releases**.
Para isso funcionar, o GitHub precisa da sua licença **gratuita** da Unity:

1. Instale o **Unity Hub**: https://unity.com/download
2. Abra o Unity Hub, entre com sua conta Unity e vá em
   **Configurações (engrenagem) → Licenses → Add → Get a free personal license**.
3. Isso cria o arquivo de licença no seu PC:
   `C:\ProgramData\Unity\Unity_lic.ulf`
   (a pasta `ProgramData` é oculta: cole o caminho direto na barra do Explorador de Arquivos).
4. No GitHub, abra o repositório → **Settings → Secrets and variables → Actions → New repository secret**
   e crie **3 segredos**:

   | Nome | Valor |
   |---|---|
   | `UNITY_LICENSE` | Todo o conteúdo do arquivo `Unity_lic.ulf` (abra com o Bloco de Notas e copie tudo) |
   | `UNITY_EMAIL` | O e-mail da sua conta Unity |
   | `UNITY_PASSWORD` | A senha da sua conta Unity |

   > ⚠️ **O login precisa funcionar com e-mail e senha.** Se a sua conta Unity foi criada com
   > **"Entrar com Google/Apple/Facebook"**, ela não tem senha: entre em https://id.unity.com,
   > use **"Esqueci minha senha"** com o seu e-mail para criar uma, e use essa senha no segredo.
   > A **verificação em duas etapas (2FA)** também precisa estar **desligada** em id.unity.com → Security.

5. Vá na aba **Actions → Gerar .exe (Windows) → Run workflow**.
   O primeiro build leva uns 15–25 minutos; os próximos são mais rápidos.

Não precisa instalar o editor da Unity: o Hub é usado só para gerar a licença.
Os segredos ficam guardados no GitHub e ninguém consegue ver o valor depois de salvo.

## 🧱 Como o projeto é organizado

O jogo inteiro é montado **por código** quando abre — não há cenas para montar no editor.

```
Assets/
  Scripts/
    Core/     Game (monta tudo), itens/inventário, malhas low-poly, sons gerados por código
    World/    Ilha (gerador), Mar (ondas), Ambiente (céu, dia/noite), Minérios
    Player/   Movimento em 1ª pessoa, nado, oxigênio, picareta
    UI/       Interface na tela
  Resources/Shaders/   Visual: low-poly, folhas, água, céu
  Editor/BuildScript.cs   Gera a cena e o .exe (usado pelo GitHub)
.github/workflows/build.yml   Build automático
```

Unity **6000.0.72f1** (Unity 6 LTS), pipeline de renderização padrão (Built-in).

## 🗺️ Etapas

- [x] **Etapa 1** — Ilha inicial, primeira pessoa, picareta minerando, mar com ondas, céu com dia/noite, nado e oxigênio
- [x] **Etapa 2** — Mercador, moedas, compra do barco, mochila e itens coletáveis, visual novo
- [x] **Etapa 3** — Navegar, ancorar e mergulhar: âncora, recifes, pérolas, melhorias do motor
- [ ] **Etapa 4** — Peixes inimigos
- [ ] **Etapa 5** — Multiplayer (até 20) + mundo salvo entre sessões
- [ ] **Etapa 6** — Canhões, arpão, minas e PvP
- [ ] **Etapa 7** — Novas ilhas, fragmentos de mapa e zonas de perigo

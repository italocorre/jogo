# Mar Aberto — Documento de Design (GDD) v2

## 1. Visão geral
Sobrevivência e construção em mar aberto, em primeira pessoa, para jogar com amigos (até 20 por servidor, quem hospeda decide).
Mistura de **Raft** (jangada que você monta do seu jeito) com **Stranded Deep** (ilhas, coleta, crafting).
PC (Windows), Unity 6, visual realista. O mundo fica salvo entre as sessões.

## 2. Ciclo de jogo
1. Nasce em uma ilha aleatória, de mãos vazias.
2. Coleta: quebra pedras, corta árvores, pega folhas, galhos, cocos, conchas e fibras.
3. Abre o **Tab**, pesquisa receitas com materiais e fabrica ferramentas, estruturas e máquinas.
4. Monta uma jangada, sai para o mar e descobre ilhas novas com o **receptor**.
5. Finca uma **bandeira** numa ilha, constrói a base (baús, fundição, cama, plantações).
6. Evolui: motores melhores, estruturas reforçadas, armas, máquinas, gasolina e **helicóptero**.
7. Defende os baús e ataca os baús dos outros.

## 3. Mundo
- **30 ilhas** espalhadas no oceano, de tamanhos e tipos diferentes (praia, floresta, montanha, rochosa, vulcânica).
- As ilhas mais longe têm minérios melhores (cobre → ferro → ouro → titânio) e são mais perigosas.
- Cada jogador nasce em uma ilha qualquer.
- Não há mercador nem NPC nas ilhas.

## 4. Coleta
| Ação | Ferramenta | Recurso |
|---|---|---|
| Quebrar pedra | mão / picareta | pedra, sílex |
| Minerar veio | picareta | carvão, cobre, ferro… |
| Cortar árvore | machado | toras, galhos, folhas |
| Pegar no chão | mão | gravetos, folhas, cocos, conchas, fibra |
| Plantar | semente / muda | árvores e plantas crescem com o tempo |

## 5. Tab — mochila, pesquisa e fabricação
Tudo fica no **Tab**, organizado em categorias (abas com ícone):
**Ferramentas · Armas · Construção · Jangada · Máquinas · Veículos · Navegação · Comida · Itens**.

- **Pesquisa (como no Raft):** cada receita bloqueada mostra um custo em materiais. Pagou, desbloqueou para sempre.
- **Fabricar:** receitas desbloqueadas mostram os ingredientes; fabrica na hora (mão) ou exige máquina (bancada, fundição).
- Receitas mostram o que desbloqueiam a seguir (árvore de progressão visível).

## 6. Construção (jangada e ilha)
- Modo construção com martelo: grade de peças (piso, parede, pilar, escada, telhado), encaixe e pré-visualização.
- Peças em tiers: madeira → madeira reforçada → metal.
- Estruturas: baú, fundição, bancada, cama, plantação, coletor de água, refinaria de gasolina, suporte de motor, leme, âncora.
- Jangada: qualquer forma; motor e leme dão direção. Quantas jangadas quiser.
- **Construções nunca são destruídas por outros jogadores** (só os baús podem ser roubados).

## 7. Território
- **Bandeira de território:** fincada numa ilha, deixa a ilha sua (e do seu grupo).
- Só o dono constrói na ilha. Inimigos entram, abrem baús e roubam, mas não quebram nada.
- O dono recebe um alerta quando um inimigo entra no território.

## 8. Morte e cama
- Ao morrer, perde tudo o que carrega. Fica uma **mochila** no lugar, que qualquer jogador pode pegar.
- Renasce na **última cama marcada** (interagir com a cama → "Marcar como ponto de renascer").
- Sem cama: renasce numa ilha aleatória.

## 9. Navegação
- **Receptor** (fabricável, colocado na jangada ou na ilha): mostra a ilha mais próxima e todos os outros receptores (barcos) dentro do alcance.
- Todos na mesma frequência: cada receptor capta o sinal dos outros a uma certa distância (melhorias de antena aumentam o alcance).

## 10. Veículos
- **Jangada** (madeira) → motores: remo, motor a vapor (carvão), motor a gasolina.
- **Helicóptero**: receita final (titânio + peças de motor + gasolina). Gasta gasolina; aceita **arma montada**.
- **Gasolina**: refinaria (máquina) transforma óleo/carvão em gasolina.

## 11. Armas (não muito fortes)
Lança, arco, besta, pistola de pólvora, canhão para jangada, metralhadora leve para helicóptero.
Servem contra peixes e outros jogadores.

## 12. Perigos
Tubarões e peixes agressivos atacam quem está na água e mordem a jangada (sem destruí-la). Ilhas longe têm mais perigo.

## 13. Multiplayer
Servidor hospedado por um jogador (até 20). Salva o mundo, as construções e os baús.

---

## Plano de etapas (um build jogável por etapa)
1. **Coleta e Tab** — remover mercador; machado; cortar árvores (toras, galhos, folhas); pegar gravetos e fibras; Tab com categorias, pesquisa e fabricação.
2. **Construção** — martelo, peças de jangada e base com encaixe; baú, cama (renascer), fundição, bancada.
3. **Jangada** — jangada montável que flutua; motor, leme e âncora como peças; várias jangadas.
4. **Mundo com 30 ilhas** — gerador de vários tipos de ilha, nascer em ilha aleatória, receptor e mapa.
5. **Multiplayer** — servidor, até 20 jogadores, sincronizar mundo, construções, baús e jangadas; salvar.
6. **Território e PvP** — bandeira, alerta de invasor, roubo de baús, armas.
7. **Plantio, comida e máquinas** — mudas que crescem, refinaria de gasolina.
8. **Helicóptero** — receita final, gasolina e arma montada.
9. **Perigos do mar** — tubarões e peixes.
10. **Personagens realistas** (Mixamo) — jogadores animados.
11. **Sobrevivência** — fome, sede, comida, água, fogueira.
12. **Clima e vida na ilha** — chuva, tempestade, raios, coletor de chuva, caranguejos e javalis.
13. **Mapa e destroços** — mapa (M) com marcação, destroços boiando e gancho.
14. **Armas e defesa** — besta, pistola de pólvora, armaduras, ilhas distantes mais perigosas.
15. **Base** — porta, janela, cerca, lampião, rede coletora.
16. **Mergulho** — nadadeiras, tanque, máscara, lanterna, garrafas com mensagem.
17. **Horta e cozinha** — canteiro, batata, melancia, ensopado.
18. **Diário de objetivos** (J).
19. **Mar vivo** — gaivotas, golfinhos, baleias, barco abandonado à deriva.
20. **Configurações gráficas** — qualidade, distância de visão, FOV, VSync.
21. **Social** — chat (Enter), marcação (botão do meio/Z), lista de jogadores (P).
22. **Vela e vento**.
23. **Chefe do mar** — serpente marinha, armadura de escamas.
24. **Correções** — revisão geral.
25. **Mundo compartilhado** — destroços, bichos, serpente, barco à deriva e plantas iguais para todos no multiplayer.
26. **Personagem** — câmera em 3ª pessoa (V), animações de pulo e golpe.
27. **Final** — rádio de resgate, navio de resgate e tela de vitória.

# Mar Aberto — Documento de Design (GDD)

## 1. Visão geral
- **Gênero:** sobrevivência/progressão em mundo aberto no mar, primeira pessoa, multiplayer com PvP.
- **Plataforma:** PC (Windows, `.exe`).
- **Visual:** estilizado, colorido, low-poly com mar bonito (no estilo de jogos de sobrevivência no oceano), com arte própria.
- **Jogadores:** até 20 por servidor — quem hospeda escolhe o limite.
- **Progresso:** o mundo fica salvo no PC de quem hospeda e continua entre sessões.

## 2. Ciclo de jogo
1. Nasce na **Ilha do Porto** com uma **picareta de pedra**.
2. Minera rochas na ilha (pedra, carvão, cobre).
3. Vende ao **Mercador** e compra o **primeiro barco**.
4. Vai para o mar, **ancora** e mergulha para minerar no fundo (minérios melhores, peixes perigosos, oxigênio limitado).
5. Melhora o barco (motor, casco, âncora, canhões...).
6. Encontra novas ilhas, que liberam minérios, barcos e picaretas melhores.
7. Guerra: afunda e saqueia os barcos dos amigos.

## 3. Ilhas
| Ilha | Bioma | Libera | Minério principal | Barreira para chegar |
|---|---|---|---|---|
| 1. Ilha do Porto | Tropical | Bote, picareta de pedra | Pedra, Carvão, Cobre | — |
| 2. Ilha Rochosa | Montanhosa | Barco de pesca, picareta de ferro | Ferro | Correntezas (motor nv. 2) |
| 3. Ilha Vulcânica | Lava | Barco blindado, picareta de ouro | Ouro, Obsidiana | Água fervente (casco térmico) |
| 4. Ilha Gelada | Neve | Quebra-gelo, picareta de diamante | Diamante | Gelo (quebra-gelo) |
| 5. Ilha Abissal | Ruínas | Navio de guerra, picareta lendária | Cristal Abissal | Tempestade + monstro (casco e armas máximos) |

## 4. Descobrir ilhas
- **Névoa no mapa:** só aparece o que você já navegou; ilhas escondidas pela neblina.
- **Fragmentos de mapa:** chance rara ao minerar (principalmente debaixo d'água). 3–5 fragmentos revelam a **área aproximada** da próxima ilha.
- **Zonas de perigo:** cada ilha é cercada por um perigo que exige equipamento (tabela acima).
- **Combustível limitado:** cada viagem é um risco calculado.
- Quem descobre uma ilha primeiro ganha bônus.

## 5. Mineração
- Picaretas: Pedra (nv. 1) → Ferro (2) → Ouro (3) → Diamante (4) → Lendária (5).
- Cada minério exige um nível mínimo de picareta (ex.: Ferro exige nv. 2).
- Debaixo d'água: barra de oxigênio (melhorável com tanque); mais fundo = minério melhor e peixe pior.
- Rochas reaparecem depois de um tempo.

## 6. Barcos e melhorias
Barcos: Bote → Barco de pesca → Blindado → Quebra-gelo → Navio de guerra.

| Peça | Efeito |
|---|---|
| Motor | Velocidade máxima |
| Hélice | Aceleração e manobra |
| Casco | Vida e resistência às zonas de perigo |
| Âncora | Ancorar mais fundo e mais rápido |
| Tanque de combustível | Alcance |
| Porão | Carga |
| Sonar | Mostra minérios e peixes por perto |
| Suporte de armas | Mais canhões |

## 7. Inimigos do mar
- Piranhas (cardume, dano contínuo), Tubarão (ataca mergulhador), Enguia elétrica (atordoa, fundo),
  Lula gigante (agarra e danifica o barco), Monstro abissal (chefe da ilha final).
- Mais agressivos à noite e em águas fundas.

## 8. Armas (equilibradas)
Dano bom, mas recarga lenta e munição limitada (comprada ou feita com minério).

| Arma | Onde | Ponto forte | Fraqueza |
|---|---|---|---|
| Arpão | Mão | Bom contra peixe, puxa alvo | Lento, curto alcance |
| Sinalizador | Mão | Revela inimigos, fogo leve | Dano baixo |
| Canhão leve | Barco | Dano bom contra barcos | Recarga 4–5 s, tiro em arco |
| Canhão pesado | Barco | Dano alto | Recarga lenta, munição cara |
| Mina marítima | Barco | Armadilha | Some com o tempo, máx. 2 |

## 9. PvP
- Zona segura perto de cada mercador.
- Barco afundado: perde **50% da carga** (fica num baú boiando); barco e melhorias não são perdidos, só paga o reparo.
- Quem afunda muitos barcos ganha recompensa e aparece no mapa.
- Times/clãs opcionais.

## 10. Multiplayer
- Quem hospeda clica em "Hospedar"; os amigos entram pelo IP.
- Sem servidor pago: usar **Radmin VPN** (grátis) ou abrir a porta no roteador.
- O PC do host controla peixes, minérios e o mundo, e salva tudo num arquivo.

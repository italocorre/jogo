using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Multiplayer e jogo salvo. Quem hospeda manda no mundo (peças, jangadas, baús, mochilas) e salva tudo;
    /// cada jogador manda o próprio personagem. No modo sozinho, é como hospedar sem abrir a porta.
    /// </summary>
    public class NetGame : MonoBehaviour
    {
        public static NetGame Instance { get; private set; }
        public static bool IsClient => Instance != null && Instance.mode == NetMode.Client;
        public static bool IsAuthority => Instance == null || Instance.mode != NetMode.Client;

        NetMode mode;
        NetLink link;
        Game game;
        WorldSave save;
        int myId;                    // 0 = quem hospeda
        bool welcomed;
        int nextPiece = 1, nextRaft = 1, nextBag = 1;
        float stateTimer, saveTimer, timeTimer, fuelTimer, playerSaveTimer, driveTimer;

        public readonly Dictionary<int, BuildPiece> Pieces = new Dictionary<int, BuildPiece>();
        public readonly Dictionary<int, Raft> Rafts = new Dictionary<int, Raft>();
        readonly Dictionary<int, LostBackpack> bags = new Dictionary<int, LostBackpack>();
        readonly Dictionary<int, RemotePlayer> remotes = new Dictionary<int, RemotePlayer>();
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        readonly Dictionary<int, string> teams = new Dictionary<int, string>();
        readonly Dictionary<int, int> skins = new Dictionary<int, int>();
        float alertTimer;
        readonly Dictionary<(int, int), float> alerted = new Dictionary<(int, int), float>();
        readonly Dictionary<int, float> remoteDriveUntil = new Dictionary<int, float>();
        readonly HashSet<int> pendingWelcome = new HashSet<int>();
        // Árvores e rochas derrubadas (para quem entrar depois): ilha → índices
        readonly List<(int island, int index, bool tree, float until)> felled = new List<(int, int, bool, float)>();

        public int PlayerCount => mode == NetMode.Client ? remotes.Count + 1 : (link != null ? link.ClientCount : 0) + 1;
        public string ModeLabel => mode == NetMode.Solo ? "Sozinho" : mode == NetMode.Host ? "Hospedando (porta " + NetConfig.Port + ")" : "Conectado";

        // ---------- Início ----------

        public void Init(Game g)
        {
            Instance = this;
            game = g;
            mode = NetConfig.Mode;
            if (mode == NetMode.Client)
            {
                link = new NetLink();
                try
                {
                    link.Join(NetConfig.Address, NetConfig.Port);
                    link.SendToServer(new NetWriter(Msg.Hello).Int(NetConfig.Version).Str(NetConfig.PlayerName).Int(NetConfig.Skin).Str(NetConfig.Team));
                    game.Hud.Toast("Conectando a " + NetConfig.Address + "...", Hud.Info);
                }
                catch (System.Exception e)
                {
                    game.Hud.Toast("Não foi possível conectar: " + e.Message, Hud.Bad);
                    mode = NetMode.Solo;
                    link = null;
                }
                return;
            }

            // Sozinho ou hospedando: carrega o mundo salvo.
            if (NetConfig.NewWorld) WorldSave.Delete();
            save = WorldSave.Load() ?? new WorldSave();
            LoadWorld();
            if (mode == NetMode.Host)
            {
                link = new NetLink();
                try
                {
                    link.Host(NetConfig.Port, NetConfig.MaxPlayers);
                    game.Hud.Toast("Partida aberta na porta " + NetConfig.Port + " (até " + NetConfig.MaxPlayers + " jogadores).", Hud.Good);
                    string ips = LocalIPs();
                    if (ips.Length > 0) game.Hud.Toast("Seus IPs: " + ips + "  (amigos na mesma rede/VPN usam um deles)", Hud.Info);
                }
                catch (System.Exception e)
                {
                    game.Hud.Toast("Não deu para abrir a partida: " + e.Message, Hud.Bad);
                    mode = NetMode.Solo;
                    link = null;
                }
            }
            names[0] = NetConfig.PlayerName;
            teams[0] = NetConfig.Team;
            skins[0] = NetConfig.Skin;
        }

        static string LocalIPs()
        {
            try
            {
                var list = new List<string>();
                foreach (var a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a)) list.Add(a.ToString());
                return string.Join(", ", list);
            }
            catch { return ""; }
        }

        void LoadWorld()
        {
            nextPiece = Mathf.Max(1, save.nextPiece);
            nextRaft = Mathf.Max(1, save.nextRaft);
            if (game.Environment != null) game.Environment.TimeOfDay = save.timeOfDay;
            foreach (var r in save.rafts)
            {
                var raft = Raft.Create(r.pos, r.rot);
                raft.Id = r.id; raft.BoatName = r.name;
                Rafts[r.id] = raft;
            }
            foreach (var p in save.pieces)
            {
                var bp = ApplyPlace(p.id, (PieceKind)p.kind, p.raft, p.pos, p.rot, p.owner, p.fuel, p.chest);
                var h = bp != null ? bp.GetComponent<Helicopter>() : null;
                if (h != null) h.HasGun = (p.flags & 1) != 0;
            }
            foreach (var r in save.rafts) if (Rafts.TryGetValue(r.id, out var raft)) { raft.Recompute(); if (r.anchored) raft.ToggleAnchor(); }
            var me = save.FindPlayer(NetConfig.PlayerName);
            if (me != null) ApplyPlayer(me);
        }

        // ---------- Jogador local (salvar/carregar) ----------

        public PlayerSaveData CapturePlayer()
        {
            var p = game.Player;
            var d = new PlayerSaveData
            {
                name = NetConfig.PlayerName,
                hasPos = true,
                pos = p.transform.position,
                look = p.transform.forward,
                hasBed = p.HasBedRespawn,
                bedPos = p.RespawnPos,
                bedLook = p.RespawnLook,
                inv = WorldSave.Slots(game.Inventory),
                selected = game.Inventory.Selected,
                unlocked = Crafting.GetUnlocked(),
                backpack = game.BackpackLevel,
                oxygen = game.OxygenLevel,
                health = p.Health,
                hunger = p.Hunger,
                thirst = p.Thirst,
            };
            if (p.Driving != null) d.pos = p.Driving.transform.position + Vector3.up * 1.5f;
            if (game.World != null)
                foreach (var isl in game.World.Islands)
                {
                    if (isl.Discovered) d.discovered.Add(isl.Index);
                    if (isl.Revealed) d.revealed.Add(isl.Index);
                }
            return d;
        }

        public void ApplyPlayer(PlayerSaveData d)
        {
            game.BackpackLevel = d.backpack;
            game.Inventory.Resize(Inventory.HotbarSize + 12 + 6 * d.backpack);
            game.Inventory.Load(WorldSave.Stacks(d.inv));
            game.Inventory.Selected = Mathf.Clamp(d.selected, 0, Inventory.HotbarSize - 1);
            Crafting.SetUnlocked(d.unlocked);
            if (d.oxygen > 0 && game.OxygenLevel == 0) { game.OxygenLevel = d.oxygen; game.Player.OxygenSeconds *= 1.6f; }
            if (d.hasBed) game.Player.SetRespawn(d.bedPos, d.bedLook);
            if (game.World != null)
            {
                if (d.discovered != null) foreach (int i in d.discovered) if (i >= 0 && i < game.World.Islands.Count) game.World.Islands[i].Discovered = true;
                if (d.revealed != null) foreach (int i in d.revealed) if (i >= 0 && i < game.World.Islands.Count) game.World.Islands[i].Revealed = true;
            }
            if (d.hasPos) { game.Player.Hunger = Mathf.Clamp(d.hunger, 30f, 100f); game.Player.Thirst = Mathf.Clamp(d.thirst, 30f, 100f); }
            if (d.hasPos) game.Player.Teleport(d.pos + Vector3.up * 0.3f, d.look);
        }

        // ---------- Atualização ----------

        void Update()
        {
            if (link != null) Pump();
            if (link != null && (mode == NetMode.Host || welcomed)) SendHelis();
            float dt = Time.deltaTime;

            // Estado do jogador: ~15 vezes por segundo.
            stateTimer -= dt;
            if (link != null && stateTimer <= 0 && game.Player != null && (mode == NetMode.Host || welcomed))
            {
                stateTimer = 1f / 15f;
                var w = StateMsg(myId);
                if (mode == NetMode.Host) link.Broadcast(w); else link.SendToServer(w);
            }

            // Pilotando jangada como cliente: manda os comandos.
            driveTimer -= dt;
            if (mode == NetMode.Client && driveTimer <= 0 && game.Player != null && game.Player.Driving is Raft dr)
            {
                driveTimer = 0.1f;
                link.SendToServer(new NetWriter(Msg.DriveInput).Int(dr.Id).Float(dr.Throttle).Float(dr.Steer));
            }

            if (mode == NetMode.Client)
            {
                playerSaveTimer -= dt;
                if (welcomed && playerSaveTimer <= 0) { playerSaveTimer = 20f; SendPlayerSave(); }
                return;
            }

            // Quem hospeda: jangadas, relógio, combustível e salvar.
            foreach (var kv in remoteDriveUntil)
                if (Time.time > kv.Value && Rafts.TryGetValue(kv.Key, out var rr) && (game.Player == null || game.Player.Driving != rr)) rr.Driven = false;
            if (link != null)
            {
                foreach (var r in Rafts.Values)
                {
                    if (r == null) continue;
                    r.netTimer -= dt;
                    if (r.netTimer > 0) continue;
                    r.netTimer = 0.1f;
                    link.Broadcast(new NetWriter(Msg.RaftState).Int(r.Id).Vec(r.transform.position).Rot(r.transform.rotation).Bool(r.Anchor == Vessel.AnchorState.Set).Float(r.Speed));
                }
                timeTimer -= dt;
                if (timeTimer <= 0 && game.Environment != null) { timeTimer = 5f; link.Broadcast(new NetWriter(Msg.TimeSync).Float(game.Environment.TimeOfDay)); }
                fuelTimer -= dt;
                if (fuelTimer <= 0)
                {
                    fuelTimer = 3f;
                    foreach (var kv in Pieces)
                    {
                        var m = kv.Value != null ? kv.Value.GetComponent<RaftMotor>() : null;
                        if (m != null && m.Running) link.Broadcast(new NetWriter(Msg.MotorFuel).Int(kv.Key).Float(m.Fuel));
                    }
                }
            }
            saveTimer -= dt;
            if (saveTimer <= 0) { saveTimer = 60f; SaveNow(); }
            alertTimer -= dt;
            if (alertTimer <= 0) { alertTimer = 2f; CheckInvaders(); }
        }

        NetWriter StateMsg(int id)
        {
            var p = game.Player;
            var held = game.Inventory.SelectedStack;
            return new NetWriter(Msg.PlayerState).Int(id).Vec(p.transform.position).Float(p.Cam.transform.eulerAngles.y).Float(p.Cam.transform.eulerAngles.x)
                .Int(held.IsEmpty ? 0 : (int)held.Type).Bool(p.Swimming).Bool(p.Driving != null);
        }

        void OnApplicationQuit()
        {
            if (mode == NetMode.Client) { if (welcomed) SendPlayerSave(); }
            else SaveNow();
            link?.Stop();
        }

        void SendPlayerSave()
        {
            if (link == null || game.Player == null) return;
            link.SendToServer(new NetWriter(Msg.PlayerSave).Str(JsonUtility.ToJson(CapturePlayer())));
        }

        /// <summary>Salva o mundo (quem hospeda / sozinho).</summary>
        public void SaveNow()
        {
            if (mode == NetMode.Client || save == null || game.Player == null) return;
            save.timeOfDay = game.Environment != null ? game.Environment.TimeOfDay : 0.3f;
            save.nextPiece = nextPiece;
            save.nextRaft = nextRaft;
            save.rafts.Clear();
            foreach (var r in Rafts.Values)
                if (r != null && r.FloorCount > 0)
                    save.rafts.Add(new RaftSave { id = r.Id, name = r.BoatName, pos = r.transform.position, rot = r.transform.rotation, anchored = r.Anchor == Vessel.AnchorState.Set });
            save.pieces.Clear();
            foreach (var kv in Pieces)
            {
                var bp = kv.Value;
                if (bp == null) continue;
                var raft = bp.GetComponentInParent<Raft>();
                var ps = new PieceSave
                {
                    id = kv.Key, kind = (int)bp.Kind, raft = raft != null ? raft.Id : -1, owner = bp.Owner,
                    pos = raft != null ? raft.transform.InverseTransformPoint(bp.transform.position) : bp.transform.position,
                    rot = raft != null ? Quaternion.Inverse(raft.transform.rotation) * bp.transform.rotation : bp.transform.rotation,
                };
                var m = bp.GetComponent<RaftMotor>();
                if (m != null) ps.fuel = m.Fuel;
                var sap = bp.GetComponent<Sapling>();
                if (sap != null) ps.fuel = sap.Age;
                var heli = bp.GetComponent<Helicopter>();
                if (heli != null) { ps.fuel = heli.Fuel; ps.flags = heli.HasGun ? 1 : 0; }
                var c = bp.GetComponent<Chest>();
                if (c != null && c.Storage != null) ps.chest = WorldSave.Slots(c.Storage);
                save.pieces.Add(ps);
            }
            save.StorePlayer(CapturePlayer());
            save.Write();
        }

        // ---------- Mensagens ----------

        void Pump()
        {
            if (link.IsServer)
            {
                while (link.TryNewConnection(out int id)) pendingWelcome.Add(id);
                while (link.TryDisconnected(out int id)) OnClientLeft(id);
            }
            else if (!link.Connected)
            {
                game.Hud.Toast("Conexão com a partida perdida.", Hud.Bad);
                link = null;
                mode = NetMode.Solo;
                return;
            }
            int budget = 400;
            while (budget-- > 0 && link != null && link.TryReceive(out var m))
            {
                try { Handle(m); }
                catch (System.Exception e) { Debug.LogWarning("Mensagem " + m.Type + " com erro: " + e.Message); }
            }
        }

        void Handle(NetReader m)
        {
            bool server = mode == NetMode.Host;
            switch (m.Type)
            {
                case Msg.Hello when server:
                {
                    int ver = m.Int(); string name = m.Str(); int skin = m.Int(); string team = m.Str();
                    if (ver != NetConfig.Version) { link.SendTo(m.From, new NetWriter(Msg.Reject).Str("Versão diferente do jogo. Baixe a mesma versão de quem hospeda.")); link.Kick(m.From); return; }
                    foreach (var kv in names) if (kv.Value == name) { link.SendTo(m.From, new NetWriter(Msg.Reject).Str("Já tem um jogador com o nome " + name + ".")); link.Kick(m.From); return; }
                    names[m.From] = name;
                    teams[m.From] = team;
                    skins[m.From] = skin;
                    pendingWelcome.Remove(m.From);
                    SendWelcome(m.From, name);
                    link.Broadcast(new NetWriter(Msg.PlayerJoined).Int(m.From).Str(name).Int(skin).Str(team), m.From);
                    SpawnRemote(m.From, name);
                    game.Hud.Toast(name + " entrou na partida.", Hud.Gold);
                    break;
                }
                case Msg.Welcome when !server:
                {
                    myId = m.Int();
                    if (game.Environment != null) game.Environment.TimeOfDay = m.Float();
                    int nPlayers = m.Int();
                    for (int i = 0; i < nPlayers; i++) { int id = m.Int(); string n = m.Str(); skins[id] = m.Int(); teams[id] = m.Str(); names[id] = n; if (id != myId) SpawnRemote(id, n); }
                    string json = m.Str();
                    if (!string.IsNullOrEmpty(json)) ApplyPlayer(JsonUtility.FromJson<PlayerSaveData>(json));
                    welcomed = true;
                    game.Hud.Toast("Conectado! " + nPlayers + " jogador" + (nPlayers > 1 ? "es" : "") + " na partida.", Hud.Good);
                    break;
                }
                case Msg.Reject when !server:
                    game.Hud.Toast("Entrada recusada: " + m.Str(), Hud.Bad);
                    link.Stop(); link = null; mode = NetMode.Solo;
                    break;
                case Msg.PlayerJoined when !server:
                {
                    int id = m.Int(); string n = m.Str(); skins[id] = m.Int(); teams[id] = m.Str(); names[id] = n;
                    if (id != myId) SpawnRemote(id, n);
                    game.Hud.Toast(n + " entrou na partida.", Hud.Gold);
                    break;
                }
                case Msg.PlayerLeft when !server:
                    RemoveRemote(m.Int());
                    break;
                case Msg.PlayerState:
                {
                    int id = server ? m.From : m.Int();
                    if (server) m.Int();
                    Vector3 pos = m.Vec(); float yaw = m.Float(), pitch = m.Float(); int held = m.Int(); bool swim = m.Bool(), drive = m.Bool();
                    if (remotes.TryGetValue(id, out var rp)) rp.SetState(pos, yaw, pitch, (ItemType)held, swim, drive);
                    if (server) link.Broadcast(new NetWriter(Msg.PlayerState).Int(id).Vec(pos).Float(yaw).Float(pitch).Int(held).Bool(swim).Bool(drive), id);
                    break;
                }
                case Msg.PlaceRequest when server:
                {
                    var k = (PieceKind)m.Int(); int raftId = m.Int(); bool newRaft = m.Bool(); Vector3 pos = m.Vec(); Quaternion rot = m.Rot();
                    HostPlace(k, raftId, newRaft, pos, rot, teams.TryGetValue(m.From, out var on) ? on : "");
                    break;
                }
                case Msg.RaftCreated when !server:
                {
                    int id = m.Int(); string n = m.Str(); Vector3 pos = m.Vec(); Quaternion rot = m.Rot();
                    if (!Rafts.ContainsKey(id)) { var r = Raft.Create(pos, rot); r.Id = id; r.BoatName = n; r.MakeRemote(); Rafts[id] = r; }
                    break;
                }
                case Msg.PiecePlaced when !server:
                {
                    int id = m.Int(); var k = (PieceKind)m.Int(); int raftId = m.Int(); Vector3 pos = m.Vec(); Quaternion rot = m.Rot(); string owner = m.Str(); float fuel = m.Float();
                    ApplyPlace(id, k, raftId, pos, rot, owner, fuel, null);
                    if (raftId >= 0 && Rafts.TryGetValue(raftId, out var r)) r.Recompute();
                    break;
                }
                case Msg.RemoveRequest when server:
                    HostRemove(m.Int());
                    break;
                case Msg.PieceRemoved when !server:
                    ApplyRemove(m.Int());
                    break;
                case Msg.RaftState when !server:
                {
                    int id = m.Int(); Vector3 pos = m.Vec(); Quaternion rot = m.Rot(); bool anch = m.Bool(); float speed = m.Float();
                    if (Rafts.TryGetValue(id, out var r) && r != null) r.SetRemoteState(pos, rot, anch, speed);
                    break;
                }
                case Msg.DriveInput when server:
                {
                    int id = m.Int(); float thr = m.Float(), steer = m.Float();
                    if (Rafts.TryGetValue(id, out var r) && r != null) { r.Driven = true; r.Throttle = thr; r.Steer = steer; remoteDriveUntil[id] = Time.time + 0.6f; }
                    break;
                }
                case Msg.AnchorToggle when server:
                    if (Rafts.TryGetValue(m.Int(), out var ar) && ar != null) ar.ToggleAnchor();
                    break;
                case Msg.Refuel when server:
                {
                    int id = m.Int(); float add = m.Float();
                    if (Pieces.TryGetValue(id, out var bp) && bp != null) { var mo = bp.GetComponent<RaftMotor>(); if (mo != null) { mo.Fuel += add; link.Broadcast(new NetWriter(Msg.MotorFuel).Int(id).Float(mo.Fuel)); } }
                    break;
                }
                case Msg.MotorFuel when !server:
                {
                    int id = m.Int(); float f = m.Float();
                    if (Pieces.TryGetValue(id, out var bp) && bp != null) { var mo = bp.GetComponent<RaftMotor>(); if (mo != null) mo.Fuel = f; }
                    break;
                }
                case Msg.TreeFelled:
                case Msg.OreBroken:
                {
                    int isl = m.Int(), idx = m.Int(); Vector3 axis = m.Type == Msg.TreeFelled ? m.Vec() : Vector3.zero;
                    ApplyFelled(isl, idx, m.Type == Msg.TreeFelled, axis);
                    if (server)
                    {
                        RememberFelled(isl, idx, m.Type == Msg.TreeFelled);
                        var w = new NetWriter(m.Type).Int(isl).Int(idx);
                        if (m.Type == Msg.TreeFelled) w.Vec(axis);
                        link.Broadcast(w, m.From);
                    }
                    break;
                }
                case Msg.ChestOpen when server:
                {
                    int id = m.Int();
                    if (Pieces.TryGetValue(id, out var bp) && bp != null) { var c = bp.GetComponent<Chest>(); if (c != null) link.SendTo(m.From, ChestMsg(id, c)); }
                    break;
                }
                case Msg.ChestContents when !server:
                {
                    int id = m.Int(); int n = m.Int();
                    var list = new List<ItemStack>();
                    for (int i = 0; i < n; i++) list.Add(new ItemStack((ItemType)m.Int(), m.Int()));
                    if (Pieces.TryGetValue(id, out var bp) && bp != null) { var c = bp.GetComponent<Chest>(); if (c != null) { c.Storage.Load(list); c.Synced = true; } }
                    break;
                }
                case Msg.ChestTake when server:
                {
                    int id = m.Int(), slot = m.Int();
                    if (!Pieces.TryGetValue(id, out var bp) || bp == null) break;
                    var c = bp.GetComponent<Chest>();
                    if (c == null || slot < 0 || slot >= c.Storage.Size) break;
                    var st = c.Storage.TakeFromSlot(slot, 999);
                    if (!st.IsEmpty) link.SendTo(m.From, new NetWriter(Msg.ChestGive).Int((int)st.Type).Int(st.Count));
                    link.Broadcast(ChestMsg(id, c));
                    break;
                }
                case Msg.ChestPut when server:
                {
                    int id = m.Int(); var t = (ItemType)m.Int(); int n = m.Int();
                    if (!Pieces.TryGetValue(id, out var bp) || bp == null) { link.SendTo(m.From, new NetWriter(Msg.ChestGive).Int((int)t).Int(n)); break; }
                    var c = bp.GetComponent<Chest>();
                    int left = c != null ? c.Storage.Add(t, n, false) : n;
                    if (left > 0) link.SendTo(m.From, new NetWriter(Msg.ChestGive).Int((int)t).Int(left));
                    if (c != null) link.Broadcast(ChestMsg(id, c));
                    break;
                }
                case Msg.ChestGive when !server:
                {
                    var t = (ItemType)m.Int(); int n = m.Int();
                    int left = game.Inventory.Add(t, n, false);
                    if (left > 0 && game.Player != null) WorldItem.Spawn(new ItemStack(t, left), game.Player.transform.position + Vector3.up, Vector3.up);
                    break;
                }
                case Msg.BackpackDrop when server:
                {
                    Vector3 pos = m.Vec(); int n = m.Int();
                    var items = new List<ItemStack>();
                    for (int i = 0; i < n; i++) items.Add(new ItemStack((ItemType)m.Int(), m.Int()));
                    HostSpawnBag(pos, items, names.TryGetValue(m.From, out var on) ? on : "?");
                    break;
                }
                case Msg.BackpackSpawned when !server:
                {
                    int id = m.Int(); Vector3 pos = m.Vec(); string owner = m.Str(); int n = m.Int();
                    var items = new List<ItemStack>();
                    for (int i = 0; i < n; i++) items.Add(new ItemStack((ItemType)m.Int(), m.Int()));
                    var b = LostBackpack.Spawn(pos, items, 0);
                    b.NetId = id; b.Owner = owner;
                    bags[id] = b;
                    break;
                }
                case Msg.BackpackTake when server:
                {
                    int id = m.Int();
                    if (bags.TryGetValue(id, out var b) && b != null)
                    {
                        foreach (var s in b.Items) link.SendTo(m.From, new NetWriter(Msg.ChestGive).Int((int)s.Type).Int(s.Count));
                        RemoveBag(id);
                        link.Broadcast(new NetWriter(Msg.BackpackTaken).Int(id));
                    }
                    break;
                }
                case Msg.BackpackTaken when !server:
                    RemoveBag(m.Int());
                    break;
                case Msg.TimeSync when !server:
                    if (game.Environment != null) game.Environment.TimeOfDay = m.Float();
                    break;
                case Msg.PlayerSave when server:
                {
                    var d = JsonUtility.FromJson<PlayerSaveData>(m.Str());
                    if (d != null && names.TryGetValue(m.From, out var on)) { d.name = on; save.StorePlayer(d); }
                    break;
                }
                case Msg.Damage:
                {
                    int target = m.Int(); float amount = m.Float(); string from = m.Str();
                    if (server && target != 0) { link.SendTo(target, new NetWriter(Msg.Damage).Int(target).Float(amount).Str(from)); break; }
                    TakeHit(amount, from);
                    break;
                }
                case Msg.Swing:
                {
                    int id = server ? m.From : m.Int(); bool hit = m.Bool();
                    if (remotes.TryGetValue(id, out var rp) && rp != null) rp.PlayAction(hit);
                    if (server) link.Broadcast(new NetWriter(Msg.Swing).Int(id).Bool(hit), id);
                    break;
                }
                case Msg.Alert when !server:
                    game.Hud.Toast(m.Str(), Hud.Bad);
                    Sfx.UI(Sfx.Error, 0.6f);
                    break;
                case Msg.HeliState:
                {
                    int id = m.Int(); Vector3 pos = m.Vec(); Quaternion rot = m.Rot(); float rotor = m.Float(), fuel = m.Float(); bool gunOn = m.Bool();
                    if (Pieces.TryGetValue(id, out var bp) && bp != null) bp.GetComponent<Helicopter>()?.ApplyNet(pos, rot, rotor, fuel, gunOn);
                    if (server) link.Broadcast(new NetWriter(Msg.HeliState).Int(id).Vec(pos).Rot(rot).Float(rotor).Float(fuel).Bool(gunOn), m.From);
                    break;
                }
                case Msg.Shot:
                {
                    Vector3 a = m.Vec(), b = m.Vec();
                    ShotFx(a, b);
                    if (server) link.Broadcast(new NetWriter(Msg.Shot).Vec(a).Vec(b), m.From);
                    break;
                }
                case Msg.Projectile:
                {
                    int k = m.Int(); Vector3 a = m.Vec(), v = m.Vec();
                    MarAberto.Projectile.Fire((MarAberto.Projectile.Kind)k, a, v, true);
                    if (server) link.Broadcast(new NetWriter(Msg.Projectile).Int(k).Vec(a).Vec(v), m.From);
                    break;
                }
                case Msg.Chat:
                {
                    string text = m.Str();
                    game.Hud.Toast(text, Hud.Info);
                    if (server) link.Broadcast(new NetWriter(Msg.Chat).Str(text), m.From);
                    break;
                }
            }
        }

        void SendWelcome(int id, string name)
        {
            var w = new NetWriter(Msg.Welcome).Int(id).Float(game.Environment != null ? game.Environment.TimeOfDay : 0.3f);
            w.Int(names.Count);
            foreach (var kv in names) w.Int(kv.Key).Str(kv.Value).Int(skins.TryGetValue(kv.Key, out var sk) ? sk : 0).Str(teams.TryGetValue(kv.Key, out var tm) ? tm : kv.Value);
            var saved = save.FindPlayer(name);
            w.Str(saved != null ? JsonUtility.ToJson(saved) : "");
            // Mundo atual: jangadas, peças, mochilas, árvores e rochas derrubadas.
            foreach (var r in Rafts.Values)
                if (r != null) link.SendTo(id, new NetWriter(Msg.RaftCreated).Int(r.Id).Str(r.BoatName).Vec(r.transform.position).Rot(r.transform.rotation));
            foreach (var kv in Pieces)
                if (kv.Value != null) link.SendTo(id, PlacedMsg(kv.Key, kv.Value));
            foreach (var kv in bags)
                if (kv.Value != null) link.SendTo(id, BagMsg(kv.Key, kv.Value));
            foreach (var f in felled)
                if (Time.time < f.until)
                {
                    var fw = new NetWriter(f.tree ? Msg.TreeFelled : Msg.OreBroken).Int(f.island).Int(f.index);
                    if (f.tree) fw.Vec(Vector3.right);
                    link.SendTo(id, fw);
                }
            link.SendTo(id, w); // por último: o cliente só começa depois de receber o mundo
        }

        void OnClientLeft(int id)
        {
            pendingWelcome.Remove(id);
            if (names.TryGetValue(id, out var n))
            {
                game.Hud.Toast(n + " saiu da partida.", Hud.Info);
                names.Remove(id);
                teams.Remove(id);
            }
            RemoveRemote(id);
            link.Broadcast(new NetWriter(Msg.PlayerLeft).Int(id));
        }

        void SpawnRemote(int id, string name)
        {
            if (remotes.ContainsKey(id)) return;
            remotes[id] = RemotePlayer.Create(id, name, skins.TryGetValue(id, out var sk) ? sk : id);
        }

        void RemoveRemote(int id)
        {
            if (remotes.TryGetValue(id, out var r) && r != null) Destroy(r.gameObject);
            remotes.Remove(id);
        }

        // ---------- Peças ----------

        NetWriter PlacedMsg(int id, BuildPiece bp)
        {
            var raft = bp.GetComponentInParent<Raft>();
            Vector3 pos = raft != null ? raft.transform.InverseTransformPoint(bp.transform.position) : bp.transform.position;
            Quaternion rot = raft != null ? Quaternion.Inverse(raft.transform.rotation) * bp.transform.rotation : bp.transform.rotation;
            var m = bp.GetComponent<RaftMotor>();
            var sap = bp.GetComponent<Sapling>();
            var hc = bp.GetComponent<Helicopter>();
            if (hc != null) return new NetWriter(Msg.PiecePlaced).Int(id).Int((int)bp.Kind).Int(raft != null ? raft.Id : -1).Vec(pos).Rot(rot).Str(bp.Owner).Float(hc.Fuel);
            return new NetWriter(Msg.PiecePlaced).Int(id).Int((int)bp.Kind).Int(raft != null ? raft.Id : -1).Vec(pos).Rot(rot).Str(bp.Owner).Float(m != null ? m.Fuel : sap != null ? sap.Age : 0f);
        }

        /// <summary>Construir: quem hospeda cria e avisa todos; o cliente pede para quem hospeda.</summary>
        public static void RequestPlace(PieceKind k, Raft raft, bool newRaft, Vector3 worldPos, Quaternion worldRot)
        {
            var n = Instance;
            Vector3 pos = raft != null && !newRaft ? raft.transform.InverseTransformPoint(worldPos) : worldPos;
            Quaternion rot = raft != null && !newRaft ? Quaternion.Inverse(raft.transform.rotation) * worldRot : worldRot;
            if (n != null && n.mode == NetMode.Client)
                n.link.SendToServer(new NetWriter(Msg.PlaceRequest).Int((int)k).Int(raft != null ? raft.Id : -1).Bool(newRaft).Vec(pos).Rot(rot));
            else if (n != null)
                n.HostPlace(k, raft != null ? raft.Id : -1, newRaft, pos, rot, NetConfig.Team);
        }

        void HostPlace(PieceKind k, int raftId, bool newRaft, Vector3 pos, Quaternion rot, string owner)
        {
            if (newRaft)
            {
                var r = Raft.Create(pos, rot);
                r.Id = nextRaft++;
                Rafts[r.Id] = r;
                raftId = r.Id;
                pos = Vector3.zero; rot = Quaternion.identity;
                link?.Broadcast(new NetWriter(Msg.RaftCreated).Int(r.Id).Str(r.BoatName).Vec(r.transform.position).Rot(r.transform.rotation));
            }
            int id = nextPiece++;
            var bp = ApplyPlace(id, k, raftId, pos, rot, owner, 0f, null);
            if (bp == null) return;
            if (raftId >= 0 && Rafts.TryGetValue(raftId, out var raft)) raft.Recompute();
            link?.Broadcast(PlacedMsg(id, bp));
        }

        BuildPiece ApplyPlace(int id, PieceKind k, int raftId, Vector3 pos, Quaternion rot, string owner, float fuel, List<SlotSave> chest)
        {
            if (Pieces.ContainsKey(id)) return Pieces[id];
            Transform parent = game.Build != null ? game.Build.Root : null;
            Vector3 wp = pos; Quaternion wr = rot;
            if (raftId >= 0)
            {
                if (!Rafts.TryGetValue(raftId, out var raft) || raft == null) return null;
                parent = raft.transform;
                wp = raft.transform.TransformPoint(pos);
                wr = raft.transform.rotation * rot;
            }
            var go = Building.Create(k, wp, wr, parent);
            var bp = go.GetComponent<BuildPiece>();
            bp.Id = id;
            bp.Owner = owner;
            if (game.Build != null) bp.Recipe = game.Build.RecipeFor(k);
            var m = go.GetComponent<RaftMotor>();
            if (m != null) m.Fuel = fuel;
            var sap = go.GetComponent<Sapling>();
            if (sap != null) sap.Age = fuel;
            var heli = go.GetComponent<Helicopter>();
            if (heli != null) heli.Fuel = fuel;
            var c = go.GetComponent<Chest>();
            if (c != null && chest != null && chest.Count > 0) { if (c.Storage == null) c.Storage = new Inventory(Chest.Size); c.Storage.Load(WorldSave.Stacks(chest)); }
            Pieces[id] = bp;
            if (k == PieceKind.Cama && owner == NetConfig.Team && game.Player != null && !game.Player.HasBedRespawn)
                go.GetComponent<Bed>()?.Interact(game.Player);
            return bp;
        }

        /// <summary>Jangada pronta para testes (F2/F3), criada pelo caminho normal (aparece para todos).</summary>
        public Raft HostTestRaft(Vector3 pos, Quaternion rot, int coal)
        {
            HostPlace(PieceKind.Piso, -1, true, pos, rot, NetConfig.PlayerName);
            var raft = Rafts[nextRaft - 1];
            for (int x = -1; x <= 1; x++)
                for (int z = 0; z < 2; z++)
                    if (x != 0 || z != 0) HostPlace(PieceKind.Piso, raft.Id, false, new Vector3(x * Building.Cell, 0, z * Building.Cell), Quaternion.identity, NetConfig.PlayerName);
            HostPlace(PieceKind.Leme, raft.Id, false, new Vector3(0, Building.FloorH, -0.4f), Quaternion.identity, NetConfig.PlayerName);
            HostPlace(PieceKind.Motor, raft.Id, false, new Vector3(0, Building.FloorH, -0.95f), Quaternion.Euler(0, 180, 0), NetConfig.PlayerName);
            HostPlace(PieceKind.Ancora, raft.Id, false, new Vector3(0, Building.FloorH, Building.Cell + 0.6f), Quaternion.identity, NetConfig.PlayerName);
            foreach (var m in raft.GetComponentsInChildren<RaftMotor>()) m.Fuel = coal * 60f;
            raft.Recompute();
            return raft;
        }

        public static void RequestRemove(BuildPiece bp)
        {
            var n = Instance;
            if (n == null) { Object.Destroy(bp.gameObject); return; }
            if (n.mode == NetMode.Client) n.link.SendToServer(new NetWriter(Msg.RemoveRequest).Int(bp.Id));
            else n.HostRemove(bp.Id);
        }

        void HostRemove(int id)
        {
            ApplyRemove(id);
            link?.Broadcast(new NetWriter(Msg.PieceRemoved).Int(id));
        }

        void ApplyRemove(int id)
        {
            if (!Pieces.TryGetValue(id, out var bp)) return;
            Pieces.Remove(id);
            if (bp == null) return;
            var raft = bp.GetComponentInParent<Raft>();
            bp.transform.SetParent(null, true);
            Destroy(bp.gameObject);
            if (raft != null)
            {
                raft.Recompute();
                if (raft.FloorCount == 0)
                {
                    foreach (var other in raft.GetComponentsInChildren<BuildPiece>()) Pieces.Remove(other.Id);
                    if (game.Player != null && game.Player.Driving == raft) game.Player.StopDriving();
                    Rafts.Remove(raft.Id);
                    Destroy(raft.gameObject);
                }
            }
        }

        // ---------- Jangada ----------

        public static void RequestAnchor(Raft r)
        {
            var n = Instance;
            if (n != null && n.mode == NetMode.Client) n.link.SendToServer(new NetWriter(Msg.AnchorToggle).Int(r.Id));
        }

        public static void RequestRefuel(RaftMotor m, float seconds)
        {
            var n = Instance;
            var bp = m.GetComponent<BuildPiece>();
            if (n != null && n.mode == NetMode.Client && bp != null) n.link.SendToServer(new NetWriter(Msg.Refuel).Int(bp.Id).Float(seconds));
            else m.Fuel += seconds;
        }

        // ---------- Árvores e rochas ----------

        public static void TreeFelledLocal(Choppable c, Vector3 axis)
        {
            var n = Instance;
            if (n == null || n.link == null || c.Island == null || c.Index < 0) { n?.RememberFelled(c.Island != null ? c.Island.Index : -1, c.Index, true); return; }
            var w = new NetWriter(Msg.TreeFelled).Int(c.Island.Index).Int(c.Index).Vec(axis);
            if (n.mode == NetMode.Client) n.link.SendToServer(w);
            else { n.RememberFelled(c.Island.Index, c.Index, true); n.link.Broadcast(w); }
        }

        public static void OreBrokenLocal(Mineable mi)
        {
            var n = Instance;
            if (n == null || n.link == null || mi.Island == null || mi.Index < 0) return;
            var w = new NetWriter(Msg.OreBroken).Int(mi.Island.Index).Int(mi.Index);
            if (n.mode == NetMode.Client) n.link.SendToServer(w);
            else { n.RememberFelled(mi.Island.Index, mi.Index, false); n.link.Broadcast(w); }
        }

        void RememberFelled(int island, int index, bool tree)
        {
            if (island < 0) return;
            felled.RemoveAll(f => Time.time > f.until);
            felled.Add((island, index, tree, Time.time + (tree ? 470f : 85f)));
        }

        void ApplyFelled(int island, int index, bool tree, Vector3 axis)
        {
            var w = game.World;
            if (w == null || island < 0 || island >= w.Islands.Count) return;
            var isl = w.Islands[island];
            if (!isl.Generated) { (tree ? isl.PendingFelled : isl.PendingBroken).Add(index); return; }
            if (tree) { if (index >= 0 && index < isl.Trees.Count) isl.Trees[index].RemoteFell(axis); }
            else if (index >= 0 && index < isl.Ores.Count) isl.Ores[index].RemoteBreak();
        }

        // ---------- Baús ----------

        NetWriter ChestMsg(int id, Chest c)
        {
            var w = new NetWriter(Msg.ChestContents).Int(id).Int(c.Storage.Size);
            foreach (var s in c.Storage.Slots) w.Int((int)s.Type).Int(s.Count);
            return w;
        }

        public static void ChestOpened(Chest c)
        {
            var n = Instance;
            var bp = c.GetComponent<BuildPiece>();
            if (n != null && n.mode == NetMode.Client && bp != null) n.link.SendToServer(new NetWriter(Msg.ChestOpen).Int(bp.Id));
        }

        /// <summary>Tirar um espaço do baú para a mochila.</summary>
        public static void ChestTake(Chest c, int slot)
        {
            var n = Instance;
            var bp = c.GetComponent<BuildPiece>();
            if (n != null && n.mode == NetMode.Client && bp != null) { n.link.SendToServer(new NetWriter(Msg.ChestTake).Int(bp.Id).Int(slot)); return; }
            var st = c.Storage.Slots[slot];
            if (st.IsEmpty) return;
            int left = n != null ? n.game.Inventory.Add(st.Type, st.Count, false) : st.Count;
            int moved = st.Count - left;
            if (moved > 0) c.Storage.TakeFromSlot(slot, moved);
            if (n != null && n.link != null && bp != null) n.link.Broadcast(n.ChestMsg(bp.Id, c));
        }

        /// <summary>Guardar um espaço da mochila no baú.</summary>
        public static void ChestPut(Chest c, int invSlot)
        {
            var n = Instance;
            if (n == null) return;
            var inv = n.game.Inventory;
            var st = inv.Slots[invSlot];
            if (st.IsEmpty) return;
            var bp = c.GetComponent<BuildPiece>();
            if (n.mode == NetMode.Client && bp != null)
            {
                inv.TakeFromSlot(invSlot, st.Count);
                n.link.SendToServer(new NetWriter(Msg.ChestPut).Int(bp.Id).Int((int)st.Type).Int(st.Count));
                return;
            }
            int left = c.Storage.Add(st.Type, st.Count, false);
            int moved = st.Count - left;
            if (moved > 0) inv.TakeFromSlot(invSlot, moved);
            else n.game.Hud.Toast("O baú está cheio.", Hud.Bad);
            if (n.link != null && bp != null) n.link.Broadcast(n.ChestMsg(bp.Id, c));
        }

        // ---------- Mochila perdida ----------

        NetWriter BagMsg(int id, LostBackpack b)
        {
            var w = new NetWriter(Msg.BackpackSpawned).Int(id).Vec(b.transform.position).Str(b.Owner).Int(b.Items.Count);
            foreach (var s in b.Items) w.Int((int)s.Type).Int(s.Count);
            return w;
        }

        /// <summary>Desmaiou: a mochila fica no chão para qualquer um pegar.</summary>
        public static void DropBackpack(Vector3 pos, List<ItemStack> items)
        {
            var n = Instance;
            if (n == null) { LostBackpack.Spawn(pos, items, 0); return; }
            if (n.mode == NetMode.Client)
            {
                var w = new NetWriter(Msg.BackpackDrop).Vec(pos).Int(items.Count);
                foreach (var s in items) w.Int((int)s.Type).Int(s.Count);
                n.link.SendToServer(w);
            }
            else n.HostSpawnBag(pos, items, NetConfig.PlayerName);
        }

        void HostSpawnBag(Vector3 pos, List<ItemStack> items, string owner)
        {
            var b = LostBackpack.Spawn(pos, items, 0);
            b.NetId = nextBag++;
            b.Owner = owner;
            bags[b.NetId] = b;
            link?.Broadcast(BagMsg(b.NetId, b));
        }

        /// <summary>Pegar a mochila: no cliente pede para quem hospeda (só um consegue).</summary>
        public static bool RequestTakeBag(LostBackpack b)
        {
            var n = Instance;
            if (n == null || n.mode != NetMode.Client || b.NetId <= 0) return false;
            n.link.SendToServer(new NetWriter(Msg.BackpackTake).Int(b.NetId));
            return true;
        }

        public static void BagTakenLocally(LostBackpack b)
        {
            var n = Instance;
            if (n == null || b.NetId <= 0) return;
            n.bags.Remove(b.NetId);
            n.link?.Broadcast(new NetWriter(Msg.BackpackTaken).Int(b.NetId));
        }

        void RemoveBag(int id)
        {
            if (bags.TryGetValue(id, out var b) && b != null) Destroy(b.gameObject);
            bags.Remove(id);
        }

        // ---------- Helicóptero ----------

        float heliTimer;
        void SendHelis()
        {
            heliTimer -= Time.deltaTime;
            if (heliTimer > 0 || link == null) return;
            heliTimer = 0.1f;
            foreach (var kv in Pieces)
            {
                if (kv.Value == null) continue;
                var h = kv.Value.GetComponent<Helicopter>();
                if (h == null || !h.LocalAuthority) continue;
                // Parado no chão e sem ninguém mexendo: manda só de vez em quando.
                bool active = h.Driven || h.Rotor > 0.01f || Time.time - h.lastLocalControl < 5f;
                if (!active && Time.time - h.lastNetSend < 1.5f) continue;
                h.lastNetSend = Time.time;
                var w = new NetWriter(Msg.HeliState).Int(kv.Key).Vec(h.transform.position).Rot(h.transform.rotation).Float(h.Rotor).Float(h.Fuel).Bool(h.HasGun);
                if (mode == NetMode.Client) link.SendToServer(w); else link.Broadcast(w);
            }
        }

        public static void SendProjectile(MarAberto.Projectile.Kind k, Vector3 from, Vector3 vel)
        {
            var n = Instance;
            if (n == null || n.link == null) return;
            var w = new NetWriter(Msg.Projectile).Int((int)k).Vec(from).Vec(vel);
            if (n.mode == NetMode.Client) n.link.SendToServer(w); else n.link.Broadcast(w);
        }

        public static void SendShot(Vector3 from, Vector3 to)
        {
            var n = Instance;
            if (n == null || n.link == null) return;
            var w = new NetWriter(Msg.Shot).Vec(from).Vec(to);
            if (n.mode == NetMode.Client) n.link.SendToServer(w); else n.link.Broadcast(w);
        }

        void ShotFx(Vector3 a, Vector3 b)
        {
            Sfx.Play(Sfx.Clank, a, 0.35f, 0.2f);
            Debris.Burst(b, (a - b).normalized, 3, new Color(0.6f, 0.55f, 0.45f));
            foreach (var kv in Pieces)
            {
                var h = kv.Value != null ? kv.Value.GetComponent<Helicopter>() : null;
                if (h != null && (h.transform.position - a).sqrMagnitude < 25f) { h.ShowTracer(a, b); break; }
            }
        }

        // ---------- PvP e território ----------

        /// <summary>Acertou outro jogador.</summary>
        public static void HitPlayer(RemotePlayer target, float amount)
        {
            var n = Instance;
            if (n == null || n.link == null) return;
            var w = new NetWriter(Msg.Damage).Int(target.Id).Float(amount).Str(NetConfig.PlayerName);
            if (n.mode == NetMode.Client) n.link.SendToServer(w); else n.link.SendTo(target.Id, w);
        }

        void TakeHit(float amount, string from)
        {
            if (game.Player == null) return;
            game.Player.Damage(amount);
            game.Hud.Toast(from + " te acertou!", Hud.Bad);
            SendSwing(true);
        }

        /// <summary>Animação de golpe/levar golpe para os outros verem.</summary>
        public static void SendSwing(bool hit)
        {
            var n = Instance;
            if (n == null || n.link == null) return;
            if (n.mode == NetMode.Client) n.link.SendToServer(new NetWriter(Msg.Swing).Int(n.myId).Bool(hit));
            else n.link.Broadcast(new NetWriter(Msg.Swing).Int(0).Bool(hit));
        }

        /// <summary>Equipe dona da ilha (bandeira), ou null.</summary>
        public string ClaimOf(Island isl)
        {
            if (isl == null) return null;
            foreach (var bp in Pieces.Values)
                if (bp != null && bp.Kind == PieceKind.Bandeira && game.World.IslandAt(bp.transform.position) == isl) return bp.Owner;
            return null;
        }

        /// <summary>Quem hospeda: avisa a equipe dona quando um inimigo entra no território.</summary>
        void CheckInvaders()
        {
            if (game.World == null) return;
            var claims = new List<(Island isl, string team)>();
            foreach (var bp in Pieces.Values)
                if (bp != null && bp.Kind == PieceKind.Bandeira) { var isl = game.World.IslandAt(bp.transform.position); if (isl != null) claims.Add((isl, bp.Owner)); }
            if (claims.Count == 0) return;
            var players = new List<(int id, Vector3 pos)>();
            if (game.Player != null) players.Add((0, game.Player.transform.position));
            foreach (var kv in remotes) if (kv.Value != null) players.Add((kv.Key, kv.Value.transform.position));
            foreach (var (isl, team) in claims)
                foreach (var (id, pos) in players)
                {
                    string pt = teams.TryGetValue(id, out var t) ? t : "";
                    if (pt == team) continue;
                    Vector3 d = isl.transform.position - pos; d.y = 0;
                    if (d.magnitude > isl.Radius * 1.3f) continue;
                    if (alerted.TryGetValue((isl.Index, id), out float until) && Time.time < until) continue;
                    alerted[(isl.Index, id)] = Time.time + 60f;
                    string text = "Invasor no seu território (" + isl.IslandName + "): " + Name(id);
                    foreach (var kv in teams)
                    {
                        if (kv.Value != team) continue;
                        if (kv.Key == 0) { game.Hud.Toast(text, Hud.Bad); Sfx.UI(Sfx.Error, 0.6f); }
                        else link?.SendTo(kv.Key, new NetWriter(Msg.Alert).Str(text));
                    }
                }
        }

        public string Name(int id) => names.TryGetValue(id, out var n) ? n : "?";
        public IEnumerable<RemotePlayer> Remotes => remotes.Values;
    }
}

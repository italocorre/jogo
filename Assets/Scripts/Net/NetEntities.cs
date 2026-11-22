using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Algo do mundo que todos veem igual: quem hospeda cria, move e decide; os outros só mostram.</summary>
    public interface INetEnt
    {
        int NetId { get; set; }
        /// <summary>0 destroço, 1 bicho, 2 serpente, 3 barco à deriva, 4 navio de resgate.</summary>
        int EntType { get; }
        int EntSub { get; }
        bool Moving { get; }
        Vector3 NetPos { get; }
        float NetYaw { get; }
        int NetState { get; }
        float NetHp { get; }
        void ApplyState(Vector3 pos, float yaw, int state, float hp);
        void RemoteGone(bool dead);
        GameObject Go { get; }
    }

    /// <summary>Um jogador (o daqui ou outro) para os bichos atacarem.</summary>
    public struct NetTarget
    {
        public Vector3 Pos;
        public Vector3 Head;
        public bool Swimming;
        public RemotePlayer Remote;   // null = o jogador daqui
    }

    public partial class NetGame
    {
        public readonly Dictionary<int, INetEnt> Ents = new Dictionary<int, INetEnt>();
        int nextEnt = 1;
        float entStateTimer, flotsamPosTimer;
        static readonly List<NetTarget> targets = new List<NetTarget>();

        /// <summary>Jogadores de todos (para os bichos de quem hospeda).</summary>
        public static List<NetTarget> Targets()
        {
            targets.Clear();
            var g = Game.Instance;
            if (g != null && g.Player != null)
                targets.Add(new NetTarget { Pos = g.Player.transform.position, Head = g.Player.Cam.transform.position, Swimming = g.Player.Swimming });
            foreach (var rp in RemotePlayer.All)
                if (rp != null) targets.Add(new NetTarget { Pos = rp.transform.position, Head = rp.transform.position + Vector3.up * 1.6f, Swimming = rp.IsSwimming, Remote = rp });
            return targets;
        }

        /// <summary>O jogador mais perto de um ponto (no plano).</summary>
        public static bool Nearest(Vector3 p, out NetTarget best)
        {
            best = default;
            float bd = float.MaxValue;
            bool any = false;
            foreach (var t in Targets())
            {
                float d = (new Vector2(t.Pos.x - p.x, t.Pos.z - p.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = t; any = true; }
            }
            return any;
        }

        public static float NearestDistance(Vector3 p) => Nearest(p, out var t) ? Vector2.Distance(new Vector2(t.Pos.x, t.Pos.z), new Vector2(p.x, p.z)) : float.MaxValue;

        public static void DamageTarget(NetTarget t, float dmg, string from)
        {
            if (t.Remote == null) { Game.Instance.Player?.Damage(dmg); return; }
            HitPlayer(t.Remote, dmg, from);
        }

        // ---------- Quem hospeda ----------

        /// <summary>Registra um objeto novo (quem hospeda) e avisa todos.</summary>
        public static void Register(INetEnt e)
        {
            var n = Instance;
            if (n == null) return;
            e.NetId = n.nextEnt++;
            n.Ents[e.NetId] = e;
            n.link?.Broadcast(n.SpawnMsg(e));
        }

        NetWriter SpawnMsg(INetEnt e) =>
            new NetWriter(Msg.EntSpawn).Int(e.NetId).Int(e.EntType).Int(e.EntSub).Vec(e.NetPos).Float(e.NetYaw).Float(e.NetHp);

        /// <summary>Some com um objeto para todos (quem hospeda).</summary>
        public static void Unregister(INetEnt e, bool dead)
        {
            var n = Instance;
            if (n == null || e == null) return;
            if (n.Ents.Remove(e.NetId)) n.link?.Broadcast(new NetWriter(Msg.EntGone).Int(e.NetId).Bool(dead));
        }

        /// <summary>Esquece o objeto (ele mesmo sumiu aqui).</summary>
        public static void Forget(INetEnt e)
        {
            var n = Instance;
            if (n != null && e != null) n.Ents.Remove(e.NetId);
        }

        void SendEntities(int to)
        {
            foreach (var e in Ents.Values) if (e != null && e.Go != null) link.SendTo(to, SpawnMsg(e));
        }

        void UpdateEntities(float dt)
        {
            if (link == null) return;
            entStateTimer -= dt;
            flotsamPosTimer -= dt;
            bool moving = entStateTimer <= 0, all = flotsamPosTimer <= 0;
            if (!moving && !all) return;
            if (moving) entStateTimer = 0.2f;
            if (all) flotsamPosTimer = 3f;
            var list = new List<INetEnt>();
            foreach (var e in Ents.Values) if (e != null && e.Go != null && (e.Moving ? moving : all)) list.Add(e);
            if (list.Count == 0) return;
            var w = new NetWriter(Msg.EntState).Int(list.Count);
            foreach (var e in list) w.Int(e.NetId).Vec(e.NetPos).Float(e.NetYaw).Int(e.NetState).Float(e.NetHp);
            link.Broadcast(w);
        }

        // ---------- Cliente → quem hospeda ----------

        /// <summary>Pegar um destroço (cliente pede; quem hospeda entrega se ainda existir).</summary>
        public static void EntTake(int id) => Act(id, 0, 0f);
        /// <summary>Bater num bicho/serpente (cliente).</summary>
        public static void EntHit(int id, float dmg) => Act(id, 1, dmg);
        /// <summary>Pegar o que a rede juntou (id da peça).</summary>
        public static void NetTake(int pieceId) => Act(pieceId, 2, 0f);
        /// <summary>Chamar o navio de resgate pelo rádio (id da peça).</summary>
        public static void CallRescue(int pieceId) => Act(pieceId, 3, 0f);

        static void Act(int id, int action, float value)
        {
            var n = Instance;
            if (n == null || n.link == null || n.mode != NetMode.Client) return;
            n.link.SendToServer(new NetWriter(Msg.EntAct).Int(id).Int(action).Float(value));
        }

        /// <summary>Quem hospeda manda o prêmio para um jogador (0 = ele mesmo).</summary>
        public static void Give(int player, int type, int sub, int extra)
        {
            var n = Instance;
            if (player == 0 || n == null || n.link == null) { ApplyGive(type, sub, extra); return; }
            n.link.SendTo(player, new NetWriter(Msg.EntGive).Int(type).Int(sub).Int(extra));
        }

        static void ApplyGive(int type, int sub, int extra)
        {
            if (type == 0) FlotsamItem.Give((FlotsamKind)sub);
            else if (type == 1) LandAnimal.GiveLoot((AnimalKind)sub, extra);
        }

        void HandleWorld(NetReader m, bool server)
        {
            switch (m.Type)
            {
                case Msg.EntSpawn when !server:
                {
                    int id = m.Int(), type = m.Int(), sub = m.Int(); Vector3 pos = m.Vec(); float yaw = m.Float(), hp = m.Float();
                    if (Ents.ContainsKey(id)) break;
                    INetEnt e = null;
                    if (type == 0) e = FlotsamItem.Spawn((FlotsamKind)sub, pos);
                    else if (type == 1) e = LandAnimal.Spawn((AnimalKind)sub, pos, game.World != null ? game.World.IslandAt(pos) : null, true);
                    else if (type == 2)
                    {
                        e = SeaSerpent.SpawnAt(pos, true);
                        if (game.Player != null && Vector3.Distance(game.Player.transform.position, pos) < 250f) game.Hud.Toast("Algo enorme se mexe debaixo d'água... um TUBARÃO-REI!", Hud.Bad);
                    }
                    else if (type == 3) e = SeaEvents.SpawnDerelict(pos, sub, yaw);
                    else if (type == 4) e = RescueShip.Spawn(pos, yaw, true);
                    if (e == null) break;
                    e.NetId = id;
                    e.ApplyState(pos, yaw, 0, hp);
                    Ents[id] = e;
                    break;
                }
                case Msg.EntGone when !server:
                {
                    int id = m.Int(); bool dead = m.Bool();
                    if (Ents.TryGetValue(id, out var e)) { Ents.Remove(id); if (e != null && e.Go != null) e.RemoteGone(dead); }
                    break;
                }
                case Msg.EntState when !server:
                {
                    int count = m.Int();
                    for (int i = 0; i < count; i++)
                    {
                        int id = m.Int(); Vector3 pos = m.Vec(); float yaw = m.Float(); int st = m.Int(); float hp = m.Float();
                        if (Ents.TryGetValue(id, out var e) && e != null && e.Go != null) e.ApplyState(pos, yaw, st, hp);
                    }
                    break;
                }
                case Msg.EntGive when !server:
                    ApplyGive(m.Int(), m.Int(), m.Int());
                    break;
                case Msg.EntAct when server:
                {
                    int id = m.Int(), action = m.Int(); float value = m.Float();
                    if (action == 3)
                    {
                        if (Pieces.TryGetValue(id, out var rb) && rb != null) RescueShip.Call(rb.transform.position);
                        break;
                    }
                    if (action == 2)
                    {
                        if (Pieces.TryGetValue(id, out var bp) && bp != null) bp.GetComponent<NetCatcher>()?.TakeFor(m.From);
                        break;
                    }
                    if (!Ents.TryGetValue(id, out var e) || e == null || e.Go == null) break;
                    if (action == 0 && e is FlotsamItem f) f.TakeFor(m.From);
                    else if (action == 1)
                    {
                        if (e is LandAnimal la) la.ApplyHit(value, la.transform.position + Vector3.up * 0.4f, m.From);
                        else if (e is SeaSerpent ss) ss.ApplyHit(value, ss.HeadPos);
                    }
                    break;
                }
            }
        }
    }
}

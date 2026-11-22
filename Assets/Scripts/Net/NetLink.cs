using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace MarAberto
{
    public enum NetMode { Solo, Host, Client }

    /// <summary>Escolhas do menu inicial.</summary>
    public static class NetConfig
    {
        public static NetMode Mode = NetMode.Solo;
        public static string PlayerName = "Marujo";
        public static string Address = "127.0.0.1";
        public static int Port = 7777;
        public static int MaxPlayers = 20;
        public static bool NewWorld;
        public static int Skin;
        public static string Clan = "";
        public const int Version = 2;
        /// <summary>Equipe: o clã, ou o próprio nome se jogar sozinho.</summary>
        public static string Team => string.IsNullOrWhiteSpace(Clan) ? PlayerName : "Clã " + Clan.Trim();
    }

    /// <summary>Tipos de mensagem entre jogadores e quem hospeda.</summary>
    public enum Msg : byte
    {
        Hello = 1, Welcome, Reject, PlayerJoined, PlayerLeft, PlayerState,
        PlaceRequest, PiecePlaced, RemoveRequest, PieceRemoved, RaftCreated, RaftState, DriveInput, AnchorToggle, Refuel, MotorFuel,
        TreeFelled, OreBroken, ChestOpen, ChestContents, ChestTake, ChestPut, ChestGive,
        BackpackDrop, BackpackSpawned, BackpackTake, BackpackTaken, TimeSync, PlayerSave, Chat, Damage, Alert, Swing,
    }

    /// <summary>Escreve uma mensagem (tipo + dados) em bytes.</summary>
    public class NetWriter
    {
        readonly MemoryStream ms = new MemoryStream(256);
        public readonly BinaryWriter W;
        public NetWriter(Msg type) { W = new BinaryWriter(ms); W.Write((byte)type); }
        public NetWriter Int(int v) { W.Write(v); return this; }
        public NetWriter Float(float v) { W.Write(v); return this; }
        public NetWriter Bool(bool v) { W.Write(v); return this; }
        public NetWriter Str(string v) { W.Write(v ?? ""); return this; }
        public NetWriter Vec(Vector3 v) { W.Write(v.x); W.Write(v.y); W.Write(v.z); return this; }
        public NetWriter Rot(Quaternion q) { W.Write(q.x); W.Write(q.y); W.Write(q.z); W.Write(q.w); return this; }
        public byte[] Bytes() => ms.ToArray();
    }

    public class NetReader
    {
        readonly BinaryReader r;
        public readonly Msg Type;
        public readonly int From;
        public NetReader(byte[] data, int from)
        {
            r = new BinaryReader(new MemoryStream(data));
            Type = (Msg)r.ReadByte();
            From = from;
        }
        public int Int() => r.ReadInt32();
        public float Float() => r.ReadSingle();
        public bool Bool() => r.ReadBoolean();
        public string Str() => r.ReadString();
        public Vector3 Vec() => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public Quaternion Rot() => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    /// <summary>
    /// Conexão TCP simples: quem hospeda aceita até N jogadores; cada mensagem vai com o tamanho na frente.
    /// A leitura acontece em outra thread; o jogo processa a fila no Update.
    /// </summary>
    public class NetLink
    {
        class Conn
        {
            public int Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public readonly object WriteLock = new object();
            public volatile bool Alive = true;
        }

        public bool IsServer { get; private set; }
        public bool Connected => IsServer || (server != null && server.Alive);
        public int ClientCount { get { lock (conns) return conns.Count; } }

        readonly ConcurrentQueue<(int from, byte[] data)> inbox = new ConcurrentQueue<(int, byte[])>();
        readonly ConcurrentQueue<int> disconnected = new ConcurrentQueue<int>();
        readonly ConcurrentQueue<int> connected = new ConcurrentQueue<int>();
        readonly Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
        TcpListener listener;
        Conn server;              // no cliente: a conexão com quem hospeda
        int nextId = 1;
        int maxClients;
        volatile bool running;

        public void Host(int port, int maxPlayers)
        {
            IsServer = true;
            maxClients = Mathf.Max(0, maxPlayers - 1);
            running = true;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            new Thread(AcceptLoop) { IsBackground = true, Name = "NetAccept" }.Start();
        }

        public void Join(string address, int port)
        {
            IsServer = false;
            running = true;
            var c = new TcpClient { NoDelay = true };
            c.Connect(address, port); // pode lançar exceção (endereço errado, porta fechada)
            server = new Conn { Id = 0, Client = c, Stream = c.GetStream() };
            new Thread(() => ReadLoop(server)) { IsBackground = true, Name = "NetRead" }.Start();
        }

        void AcceptLoop()
        {
            while (running)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch { break; }
                c.NoDelay = true;
                lock (conns)
                {
                    if (conns.Count >= maxClients) { try { c.Close(); } catch { } continue; }
                    var conn = new Conn { Id = nextId++, Client = c, Stream = c.GetStream() };
                    conns[conn.Id] = conn;
                    connected.Enqueue(conn.Id);
                    new Thread(() => ReadLoop(conn)) { IsBackground = true, Name = "NetRead" + conn.Id }.Start();
                }
            }
        }

        void ReadLoop(Conn c)
        {
            var header = new byte[4];
            try
            {
                while (running && c.Alive)
                {
                    if (!ReadExact(c.Stream, header, 4)) break;
                    int len = BitConverter.ToInt32(header, 0);
                    if (len <= 0 || len > 4 * 1024 * 1024) break;
                    var data = new byte[len];
                    if (!ReadExact(c.Stream, data, len)) break;
                    inbox.Enqueue((c.Id, data));
                }
            }
            catch { }
            c.Alive = false;
            disconnected.Enqueue(c.Id);
        }

        static bool ReadExact(NetworkStream s, byte[] buf, int len)
        {
            int got = 0;
            while (got < len)
            {
                int n = s.Read(buf, got, len - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        static void Write(Conn c, byte[] data)
        {
            if (c == null || !c.Alive) return;
            try
            {
                lock (c.WriteLock)
                {
                    c.Stream.Write(BitConverter.GetBytes(data.Length), 0, 4);
                    c.Stream.Write(data, 0, data.Length);
                }
            }
            catch { c.Alive = false; }
        }

        /// <summary>Cliente → quem hospeda.</summary>
        public void SendToServer(NetWriter w) => Write(server, w.Bytes());

        /// <summary>Quem hospeda → um jogador.</summary>
        public void SendTo(int id, NetWriter w)
        {
            Conn c;
            lock (conns) conns.TryGetValue(id, out c);
            Write(c, w.Bytes());
        }

        /// <summary>Quem hospeda → todos (menos um, opcional).</summary>
        public void Broadcast(NetWriter w, int except = -1)
        {
            var bytes = w.Bytes();
            List<Conn> list;
            lock (conns) list = new List<Conn>(conns.Values);
            foreach (var c in list) if (c.Id != except) Write(c, bytes);
        }

        public void Kick(int id)
        {
            Conn c;
            lock (conns) { conns.TryGetValue(id, out c); conns.Remove(id); }
            if (c != null) { c.Alive = false; try { c.Client.Close(); } catch { } }
        }

        public bool TryNewConnection(out int id) => connected.TryDequeue(out id);

        public bool TryDisconnected(out int id)
        {
            if (!disconnected.TryDequeue(out id)) return false;
            lock (conns) conns.Remove(id);
            return true;
        }

        public bool TryReceive(out NetReader msg)
        {
            msg = null;
            if (!inbox.TryDequeue(out var item)) return false;
            try { msg = new NetReader(item.data, item.from); }
            catch { return false; }
            return true;
        }

        public void Stop()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            lock (conns) { foreach (var c in conns.Values) { try { c.Client.Close(); } catch { } } conns.Clear(); }
            try { server?.Client.Close(); } catch { }
        }
    }
}

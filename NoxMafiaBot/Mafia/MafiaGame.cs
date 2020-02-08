using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Newtonsoft.Json;

namespace Mafia
{
    public enum PlayerAlignment
    {
        Town,
        Mafia,
        ThirdParty
    }

    public enum GameState
    {
        Signups,
        Day,
        Night
    }

    [Flags] public enum PowerFlags
    {
        None = 0,
        NightKill = 1,
        Vigilante = 2,
        Strongman = 4,
        Ninja = 8,
        Janitor = 16,
        Roleblock = 32,
        InvestigationImmune = 64,
        Miller = 128,
        Frame = 256,
        Investigate = 512,
        Doctor = 1024,
        Track = 2048,
        Watch = 4096,
        Innocent = 8192,
        Paranoid = 16384,
        Jail = 32768,
        Jester = 65536,
    }

    public class Player
    {
        public SocketUser Username;
        public PlayerRole Role;
        public bool Alive = true, FlipOnDeath = true;
        public List<SocketUser> Votes;

        public Player(SocketUser user)
        {
            Username = user;
            Votes = new List<SocketUser>();
        }
    }

    public class DefaultRole
    {
        [JsonProperty("roleName")]
        public string Name;

        [JsonProperty("roleDescription")]
        public string Description;

        [JsonProperty("rolePowers")]
        public string[] Powers;

        [JsonProperty("rolePowerCharges")]
        public int[] Charges;

        [JsonProperty("roleAlignment")]
        public string Alignment;
    }

    [DataContract]
    public class DefaultRoleCollection
    {
        [JsonProperty("defaultRoles")]
        public List<DefaultRole> Roles { get; set; }
    }

    public class PlayerRole
    {
        public string Name, Description;
        public PowerFlags Powers;
        public PlayerAlignment Alignment;
        public int[] Charges;

        public PlayerRole(string RoleName, string RoleDescription, PowerFlags RolePowers, int[] PowerCharges, PlayerAlignment RoleAlignment)
        {
            Name = RoleName;
            Description = RoleDescription;
            Powers = RolePowers;
            Charges = PowerCharges;
            Alignment = RoleAlignment;
        }
    }

    public class Game
    {
        public List<Player> PlayerList, MafiaList;
        public List<PlayerRole> PlayerRoles;

        public bool Running;
        public int Players, DayCount;
        public SocketGuild Server;
        public SocketUser Mod;
        public GameState State;

        public Game(SocketGuild DiscordServer, SocketUser GameMod, int NumberOfPlayers)
        {
            Running = true;
            Server = DiscordServer;
            Mod = GameMod;
            Players = NumberOfPlayers;
            DayCount = 0;
            State = GameState.Signups;
            PlayerList = new List<Player>();
            MafiaList = new List<Player>();
            PlayerRoles = new List<PlayerRole>();
        }
    }
}

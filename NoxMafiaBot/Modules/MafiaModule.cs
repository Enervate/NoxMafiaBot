using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using NoxMafiaBot;

namespace Mafia.Modules
{
    [Name("Mafia")]
    public class MafiaModule : ModuleBase<SocketCommandContext>
    {
        public bool CheckRole(SocketGuildUser user, string RoleName)
        {
            var role = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Name == RoleName);

            // Check if user has the named role
            if (user.Roles.Contains(role))
                return true;

            return false;
        }

        public ulong GetRoleID(SocketGuild guild, string RoleName)
        {
            foreach (SocketRole role in guild.Roles)
                if (role.Name == RoleName)
                    return role.Id;

            return 0;
        }

        // *************************************************
        // The commmands below will only work during signups
        // *************************************************

        [Command("startgame")]
        [Summary("Start the game")]
        [RequireContext(ContextType.Guild)]
        public async Task StartGame()
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if(CurrentGame.State != GameState.Signups)
            {
                await Context.User.SendMessageAsync($"Current game in {CurrentGame.Server.Name} has already started!");
                return;
            }

            if(CurrentGame.PlayerList.Count < CurrentGame.Players)
            {
                await Context.User.SendMessageAsync($"Current game in {CurrentGame.Server.Name} has fewer players signed up than the required {CurrentGame.Players}, please get more people to sign up or adjust the number of players with the !numplayers command.");
                return;
            }

            CurrentGame.State = GameState.Day;

            //DOSTUFF to start the game
        }

        [Command("startsignups")]
        [Summary("Start signups for a new game")]
        [RequireContext(ContextType.Guild)]
        public async Task BeginSignups([Remainder]int Players)
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            bool gameRunning = false;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is not running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                {
                    await ReplyAsync("A game is currently running!");
                    gameRunning = true;
                }
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString())) // For now, only allow a user to start a game on one server at a time
                {
                    await Context.User.SendMessageAsync($"You have already started a mafia game on {Context.Guild.Name}!");
                    return;
                }
            }
            if (!gameRunning)
            {

                // Only mafia mods should be able to start games
                if (CheckRole(Context.User as SocketGuildUser, "MafiaMod"))
                {
                    Startup.Games.Add(new Game(Context.Guild, Context.User, Players)); // Instantiate a new game object for this server

                    string Mention = string.Empty;

                    if (GetRoleID(Context.Guild, "MafiaPlayer") != 0)
                        Mention = $"{MentionUtils.MentionRole(GetRoleID(Context.Guild, "MafiaPlayer"))} ";

                    if(Players < 7) // Realistically, you need at least 7 players, so the bot will not allow a game with fewer than that
                    {
                        Players = 7;

                        await ReplyAsync("The minimum number of players is seven, and has been adjusted accordingly!");
                    }

                    await ReplyAsync($"{Mention}__**Signups have begun for a new mafia game hosted by {Context.User.Username}! Number of players: {Players}**__");
                    await ReplyAsync("Please use the !sign command to sign up for this game!");
                }
                else
                    await Context.User.SendMessageAsync("You need to have the MafiaMod role in order to use this command!");
            }
        }

        [Command("cancelgame")]
        [Summary("Cancel the current game")]
        [RequireContext(ContextType.Guild)]
        public async Task EndGame()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }
            if (CurrentGame != null)
            {
                Startup.Games.Remove(CurrentGame);
                await ReplyAsync($"Current game cancelled by {Context.User.Username}.");
            }
        }

        [Command("sign")]
        [Summary("Sign up for the current game")]
        [RequireContext(ContextType.Guild)]
        public async Task Signup()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null, PlayerGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;

                foreach (Mafia.Player player in game.PlayerList) // Check to see if player is already in a game
                    if (Context.User == player.Username)
                        PlayerGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
                return;

            if(PlayerGame != null) // Currently allow only one game at a time per player
            {
                await Context.User.SendMessageAsync($"You are already signed up for a game in {PlayerGame.Server.Name}!");
                return;
            }

            if (CurrentGame != null)
            {
                if (CurrentGame.PlayerList.Count < CurrentGame.Players) // Make sure the game has room
                {
                    if (CheckRole(Context.User as SocketGuildUser, "MafiaPlayer") || CheckRole(Context.User as SocketGuildUser, "MafiaMod"))
                    {
                        CurrentGame.PlayerList.Add(new Player(Context.User)); // Instantiate a new player object for this game
                        await ReplyAsync($"Player {Context.User.Username} has signed up!");
                    }
                    else
                        await Context.User.SendMessageAsync("You need to have the MafiaPlayer or MafiaMod role in order to use this command!");
                }
                else
                    await Context.User.SendMessageAsync("Current game is full!");
            }
            else
                await ReplyAsync("There is no game running on this server!");
        }

        [Command("unsign")]
        [Summary("Remove yourself from the signups for the current game")]
        [RequireContext(ContextType.Guild)]
        public async Task Unsign()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
                return;

            if (CurrentGame != null)
            {
                if (CheckRole(Context.User as SocketGuildUser, "MafiaPlayer") || CheckRole(Context.User as SocketGuildUser, "MafiaMod"))
                {
                    foreach (Player player in CurrentGame.PlayerList)
                    {
                        if (player.Username == Context.User)
                        {
                            CurrentGame.PlayerList.Remove(player); // Remove player instance from this game
                            await ReplyAsync($"Player {Context.User.Username} is no longer signed up!");

                            return;
                        }
                    }
                    await Context.User.SendMessageAsync("You are not signed up for any games on this server!");
                }
                else
                    await Context.User.SendMessageAsync("You need to have the MafiaPlayer or MafiaMod role in order to use this command!");
            }
            else
                await ReplyAsync("There is no game running on this server!");
        }

        [Command("players")]
        [Summary("List the current players/signups for the current game")]
        [RequireContext(ContextType.Guild)]
        public async Task ListPlayers()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
                return;

            if (CurrentGame != null)
            {
                if (CurrentGame.State == GameState.Signups)
                    await ReplyAsync("Current signups:");
                else
                    await ReplyAsync("Current game players:");

                if (CheckRole(Context.User as SocketGuildUser, "MafiaPlayer") || CheckRole(Context.User as SocketGuildUser, "MafiaMod"))
                {
                    string players = string.Empty;

                    // Build a list of current players
                    foreach (Player player in CurrentGame.PlayerList)
                        players += player.Username.Username + "\n";

                    await ReplyAsync(players);
                }
                else
                    await Context.User.SendMessageAsync("You need to have the MafiaPlayer or MafiaMod role in order to use this command!");
            }
            else
                await ReplyAsync("There is no game running on this server!");
        }

        [Command("numplayers")]
        [Summary("Adjust the number of players in the game")]
        [RequireContext(ContextType.DM)]
        public async Task AdjustPlayers([Remainder]int NumberOfPlayers)
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Find the game this user started
            {
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString()))
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
            {
                await Context.User.SendMessageAsync("This game is already running!");
                return;
            }

            if (CurrentGame != null)
            {
                if (NumberOfPlayers < 7)
                {
                    await Context.User.SendMessageAsync("Game cannot be adjusted below the 7 player minimum!");
                    return;
                }

                if(NumberOfPlayers < CurrentGame.Players)
                {
                    if (CurrentGame.PlayerList.Count > NumberOfPlayers) // If the adjusted number of players is fewer than the number of signed up players, trim the signup list to match the new number of players
                    {
                        CurrentGame.PlayerList.RemoveRange(NumberOfPlayers, CurrentGame.PlayerList.Count - NumberOfPlayers);

                        await Context.User.SendMessageAsync("Warning: The adjusted number of players is fewer than the number of signups. Signups have been trimmed to match the new number of players. Use the !players command in the mafia channel to see the updated player list.");
                    }

                    if(CurrentGame.PlayerRoles.Count > NumberOfPlayers) // If the adjusted number of players is fewer than the number of roles added, trim the role list to match the new number of players
                    {
                        CurrentGame.PlayerRoles.RemoveRange(NumberOfPlayers, CurrentGame.PlayerRoles.Count - NumberOfPlayers);

                        await Context.User.SendMessageAsync("Warning: The adjusted number of players is fewer than the number of roles added to this game. The role list has been trimmed to match the new number of players. Use the !roles command to see the updated role list.");
                    }

                    CurrentGame.Players = NumberOfPlayers;

                    await Context.User.SendMessageAsync($"Number of players adjusted to {NumberOfPlayers}!");
                }
            }
            else
                await Context.User.SendMessageAsync("There is no game running on this server or you are not the mod of the current game!");
        }

        [Command("addrole")]
        [Summary("Add a role from the predefined role list to the current game")]
        [RequireContext(ContextType.DM)]
        public async Task AddRole([Remainder]string DefaultRoleName)
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Find the game this user started
            {
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString()))
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
            {
                await Context.User.SendMessageAsync("This game is already running!");
                return;
            }

            if (CurrentGame != null)
            {
                bool Found = false;

                foreach (PlayerRole Role in Startup.DefaultRoles)
                {
                    if (DefaultRoleName == Role.Name)
                    {
                        CurrentGame.PlayerRoles.Add(new PlayerRole(Role.Name, Role.Description, Role.Powers, Role.Charges, Role.Alignment));
                        Found = true;

                        await Context.User.SendMessageAsync($"Role \"{Role.Name}\" added.");
                    }
                }

                if (!Found)
                    await Context.User.SendMessageAsync($"{DefaultRoleName} not found in the list of default roles! Please consult DefaultRoles.json to ensure you are using the correct name!");
            }
            else
                await Context.User.SendMessageAsync("There is no game running on this server or you are not the mod of the current game!");
        }

        [Command("addcustomrole")]
        [Summary("Add a custom role to the current game (ex: \"!addcustomrole Doctor Miller|A doctor who is also a miller!|Doctor,Miller|-1,-1|Town\"")]
        [RequireContext(ContextType.DM)]
        public async Task AddCustomRole([Remainder]string RoleString)
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Find the game this user started
            {
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString()))
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
            {
                await Context.User.SendMessageAsync("This game is already running!");
                return;
            }

            if (CurrentGame != null)
            {
                try
                {
                    string[] newRole = RoleString.Split('|');

                    // Attempt to instantiate a new custom role within the current game
                    if (newRole.Length != 5)
                        await Context.User.SendMessageAsync("Role string is not in the proper format!\nex: !addcustomrole Doctor Miller|A doctor who is also a miller!|Doctor,Miller|-1,-1|Town");
                    else
                    {
                        Array Powers = Enum.GetValues(typeof(Mafia.PowerFlags));

                        string[] customPowers = newRole[2].Trim().Split(',');
                        Mafia.PowerFlags customPowerFlags = Mafia.PowerFlags.None;

                        // Compare each specified power to existing enumerated powers and add to the custom power flags if found
                        if (customPowers.Length > 0)
                        {
                            foreach (string thisPower in customPowers)
                            {
                                foreach (Mafia.PowerFlags val in Powers)
                                {
                                    string powerFlag = Enum.GetName(typeof(Mafia.PowerFlags), val);

                                    if (powerFlag == thisPower)
                                        customPowerFlags |= val;
                                }
                            }
                        }

                        Mafia.PlayerAlignment Alignment = Mafia.PlayerAlignment.Town; // Set alignment to town by default in case the specified alignment is invalid
                        Array Alignments = Enum.GetValues(typeof(Mafia.PlayerAlignment));

                        foreach (Mafia.PlayerAlignment val in Alignments)
                        {
                            if (Enum.GetName(typeof(Mafia.PlayerAlignment), val) == newRole[4])
                                Alignment = val;
                        }

                        // Add new custom role to current game
                        int[] Charges = newRole[3].Split(',').Select(x => int.Parse(x)).ToArray<int>();

                        CurrentGame.PlayerRoles.Add(new PlayerRole(newRole[0], newRole[1], customPowerFlags, Charges, Alignment));

                        await Context.User.SendMessageAsync($"Custom role \'{newRole[0]}\' added.");
                    }
                }
                catch (Exception err)
                {
                    await Context.User.SendMessageAsync(err.Message);
                }
            }
            else
                await Context.User.SendMessageAsync("There is no game running on this server or you are not the mod of the current game!");
        }

        [Command("removerole")]
        [Summary("Remove a role from the current game")]
        [RequireContext(ContextType.DM)]
        public async Task RemoveRole([Remainder]string RoleName)
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Find the game this user started
            {
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString()))
                    CurrentGame = game;
            }

            if (CurrentGame != null && CurrentGame.State != GameState.Signups) // Ignore command if game is already running
            {
                await Context.User.SendMessageAsync("This game is already running!");
                return;
            }

            if (CurrentGame != null)
            {
                bool Found = false;

                foreach (PlayerRole Role in CurrentGame.PlayerRoles)
                {
                    if (RoleName == Role.Name)
                    {
                        CurrentGame.PlayerRoles.Remove(Role);
                        Found = true;

                        await Context.User.SendMessageAsync($"Role \"{Role.Name}\" removed.");
                    }
                }

                if (!Found)
                    await Context.User.SendMessageAsync($"{RoleName} not found in the list of roles for this game!");
            }
        }

        // *********************************************
        // The commmands work while the game is underway
        // *********************************************

        [Command("roles")]
        [Summary("List the roles added to the current game")]
        [RequireContext(ContextType.DM)]
        public async Task ListRoles()
        {
            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Find the game this user started
            {
                if ((game.Mod.Username + game.Mod.Discriminator.ToString()) == (Context.User.Username + Context.User.Discriminator.ToString()))
                    CurrentGame = game;
            }

            if (CurrentGame != null)
            {
                string Response = "__Role list for current game__:\n";
                foreach (Mafia.PlayerRole role in CurrentGame.PlayerRoles)
                    Response += $"{role.Name}\n";

                await Context.User.SendMessageAsync(Response.TrimEnd());
            }
            else
                await Context.User.SendMessageAsync("There is no game running on this server or you are not the mod of the current game!");
        }
    }
}

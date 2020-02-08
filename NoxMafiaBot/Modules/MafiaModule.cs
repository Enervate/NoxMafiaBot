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

        public bool AddUserToScumChat(SocketGuild guild, SocketGuildUser user)
        {
            SocketGuildChannel scumChat = null;

            foreach (SocketGuildChannel channel in guild.Channels)
                if (channel.Name == "mafia-scum-chat")
                    scumChat = channel;

            if (scumChat != null)
            {
                scumChat.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow));
                return true;
            }

            return false;
        }

        public bool RemoveUserFromScumChat(SocketGuild guild, SocketGuildUser user)
        {
            SocketGuildChannel scumChat = null;

            foreach (SocketGuildChannel channel in guild.Channels)
                if (channel.Name == "mafia-scum-chat")
                    scumChat = channel;

            if (scumChat != null)
            {
                scumChat.RemovePermissionOverwriteAsync(user, options: null);
                return true;
            }

            return false;
        }

        // *************************************************
        // The commmands below will only work during signups
        // *************************************************

        [Command("purge")]
        [Summary("Purge chat")]
        [RequireContext(ContextType.Guild)]
        public async Task PurgeChat()
        {
            if (Context.Channel.Name != "mafia-scum-chat" && Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channels
                return;

            var messages = await Context.Channel.GetMessagesAsync(Context.Message, Direction.Before, 99999).FlattenAsync();
            var filteredMessages = messages.Where(x => (DateTimeOffset.UtcNow - x.Timestamp).TotalDays <= 14);

            if(filteredMessages.Count() > 0)
                await (Context.Channel as ITextChannel).DeleteMessagesAsync(filteredMessages);
        }

        [Command("startgame")][Alias("sg")]
        [Summary("Start the game")]
        [RequireContext(ContextType.Guild)]
        public async Task StartGame()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if ((CurrentGame.Mod.Username + CurrentGame.Mod.Discriminator.ToString()) != (Context.User.Username + Context.User.Discriminator.ToString())) // Only the person who started signups can start the game
            { 
                await Context.User.SendMessageAsync("You are not the mod of the current game!");
                return;
            }

            if (CurrentGame.State != GameState.Signups)
            {
                await Context.User.SendMessageAsync($"Current game in {CurrentGame.Server.Name} has already started!");
                return;
            }

            if (CurrentGame.PlayerList.Count < CurrentGame.Players)
            {
                await Context.User.SendMessageAsync($"Current game in {CurrentGame.Server.Name} has fewer players signed up than the required {CurrentGame.Players}, please get more people to sign up or adjust the number of players with the !numplayers command.");
                return;
            }

            int mafiaCount = 0; 

            foreach (Mafia.PlayerRole role in CurrentGame.PlayerRoles) // Populate the mafia list
            {
                if (role.Alignment == PlayerAlignment.Mafia)
                    mafiaCount++;
            }
            
            if (mafiaCount == 0)
            {
                await Context.User.SendMessageAsync($"Current game in {CurrentGame.Server.Name} has no Mafia-aligned roles! Please add at least one before starting the game.");
                return;
            }

            // Start the game!
            CurrentGame.State = GameState.Day;

            // If the list of roles is shorter than the list of players, fill the list of roles with vanilla town
            if (CurrentGame.PlayerRoles.Count < CurrentGame.Players)
            {
                int Count = CurrentGame.PlayerRoles.Count;
                for (int i = 0; i < (CurrentGame.Players - Count); i++)
                    CurrentGame.PlayerRoles.Add(new PlayerRole("Vanilla", "Your only power is your vote.", PowerFlags.None, new int[] { -1 }, PlayerAlignment.Town));
            }

            {
                /*  ***** DEBUG ***** 
                    Generate a list of dummy players if the player list isn't full
                    Comment this code out for release  */

                //if (CurrentGame.PlayerList.Count < CurrentGame.Players)
                //{
                //    int Count = CurrentGame.PlayerList.Count;
                //    for (int i = 0; i < (CurrentGame.Players - Count); i++)
                //        CurrentGame.PlayerList.Add(new Player(Context.User));
                //}

                /*  ***** DEBUG ***** */
            }

            // Assign a random role from the role list to each player
            var rnd = new Random();
            var roleAssignments = Enumerable.Range(0, CurrentGame.PlayerRoles.Count).OrderBy(x => rnd.Next()).Take(CurrentGame.PlayerRoles.Count).ToList();

            for(int i = 0; i < roleAssignments.Count; i++)
                CurrentGame.PlayerList[i].Role = CurrentGame.PlayerRoles[roleAssignments[i]];

            // Send out role PMs & add each scum player to scum chat
            foreach(Mafia.Player player in CurrentGame.PlayerList)
            {
                string Powers = string.Empty, PowerCharges = string.Empty;

                foreach (Mafia.PowerFlags power in Enum.GetValues(typeof(Mafia.PowerFlags)))
                {
                    if ((player.Role.Powers & power) == power && (player.Role.Powers & power) != PowerFlags.None)
                        Powers += $"{Enum.GetName(typeof(Mafia.PowerFlags), power)}, ";
                }

                foreach(int chargeval in player.Role.Charges)
                {
                    if (chargeval == -1)
                        PowerCharges += "Infinite, ";
                    else
                        PowerCharges += chargeval;
                }

                await player.Username.SendMessageAsync($"Your role is: {player.Role.Name}\nDescription: {player.Role.Description}\nPowers: {(Powers.Length > 0 ? Powers.TrimEnd(new char[] { ',', ' ' }) : "None")}\n" +
                                                       $"Power charges: {PowerCharges.TrimEnd(new char[] { ',', ' ' })}\nAlignment: {player.Role.Alignment.ToString()}");

                if (player.Role.Alignment == PlayerAlignment.Mafia)
                    AddUserToScumChat(Context.Guild, player.Username as SocketGuildUser);
            }

            // Notify the players
            string Mention = string.Empty;

            if (GetRoleID(Context.Guild, "Mafia Player") != 0)
                Mention = $" {MentionUtils.MentionRole(GetRoleID(Context.Guild, "Mafia Player"))}";

            await ReplyAsync($"Attention{Mention}! The game has started! Use ##vote <player> to cast your vote.");
        }

        [Command("startsignups")][Alias("ss")]
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
                if (CheckRole(Context.User as SocketGuildUser, "Mafia Mod"))
                {
                    Startup.Games.Add(new Game(Context.Guild, Context.User, Players)); // Instantiate a new game object for this server

                    string Mention = string.Empty;

                    if (GetRoleID(Context.Guild, "Mafia Player") != 0)
                        Mention = $"{MentionUtils.MentionRole(GetRoleID(Context.Guild, "Mafia Player"))} ";

                    await ReplyAsync($"{Mention}__**Signups have begun for a new mafia game hosted by {Context.User.Username}! Number of players: {Players}**__");
                    await ReplyAsync("Please use the ##sign command to sign up for this game!");
                }
                else
                    await Context.User.SendMessageAsync("You need to have the Mafia Mod role in order to use this command!");
            }
        }

        [Command("cancelgame")][Alias("cg")]
        [Summary("Cancel the current game")]
        [RequireContext(ContextType.Guild)]
        public async Task CancelGame()
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
                    CurrentGame.PlayerList.Add(new Player(Context.User)); // Instantiate a new player object for this game
                    await ReplyAsync($"Player {Context.User.Username} has signed up!");
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

                string players = string.Empty;

                // Build a list of current players
                foreach (Player player in CurrentGame.PlayerList)
                    players += player.Username.Username + "\n";

                await ReplyAsync(players);
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
        [Summary("Add a custom role to the current game (ex: \"##addcustomrole Doctor Miller|A doctor who is also a miller!|Miller,Doctor|-1,-1|Town\"")]
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
                        await Context.User.SendMessageAsync("Role string is not in the proper format!\nex: ##addcustomrole Doctor Miller|A doctor who is also a miller!|Miller,Doctor|-1,-1|Town");
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

        // ***************************************************
        // The commmands below work while the game is underway
        // ***************************************************

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
                string Response = string.Empty;

                if (CurrentGame.State == GameState.Signups)
                {
                    Response = "__Role list for current game__:\n";
                    foreach (Mafia.PlayerRole role in CurrentGame.PlayerRoles)
                        Response += $"{role.Name}\n";
                }
                else
                {
                    Response = "__Role list for current game__:\n";
                    foreach (Mafia.Player player in CurrentGame.PlayerList)
                        Response += $"{player.Username.Username} - {player.Role.Name}\n";
                }

                await Context.User.SendMessageAsync(Response.TrimEnd());
            }
            else
                await Context.User.SendMessageAsync("There is no game running on this server or you are not the mod of the current game!");
        }

        [Command("votecount")][Alias("vc")]
        [Summary("Get the current vote count")]
        [RequireContext(ContextType.Guild)]
        public async Task VoteCount()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if (CurrentGame == null) // Ignore if there is no game running
                return;
            else if (CurrentGame != null && CurrentGame.State == GameState.Signups) // Ignore command if game is not running
                return;
            else if (CurrentGame.State == GameState.Night) // Ignore command if it is night
                return;

            string vcResponse = string.Empty;
            List<SocketUser> hasVoted = new List<SocketUser>();

            // Count the votes against each player
            foreach(Player player in CurrentGame.PlayerList)
            {
                if(player.Alive && player.Votes.Count > 0)
                {
                    string voteList = string.Empty;
                    int voteCount = 0;
                    
                    foreach(SocketUser vote in player.Votes)
                    {
                        voteList += $"{vote.Username}, ";
                        voteCount++;

                        if (!hasVoted.Contains(vote)) //Add each unique voter to a list
                            hasVoted.Add(vote);
                    }

                    voteList = voteList.TrimEnd(new char[] { ',', ' ' });
                    vcResponse += $"**{player.Username.Username}** ({voteCount}): {voteList}";
                    vcResponse += "\n";
                }
            }

            vcResponse += "**No Vote** (";

            string noVoteList = string.Empty;
            int noVoteCount = 0;

            // Count the number of non-voters

            foreach (Player player in CurrentGame.PlayerList)
            {
                if(player.Alive && !hasVoted.Contains(player.Username))
                {
                    noVoteList += $"{player.Username.Username}, ";
                    noVoteCount++;
                }
            }

            noVoteList = noVoteList.TrimEnd(new char[] { ',', ' ' });
            vcResponse += $"{noVoteCount}): {noVoteList}";
            vcResponse += "\n";

            await ReplyAsync(vcResponse);
        }

        [Command("vote")]
        [Summary("Vote for target player to be lynched")]
        [RequireContext(ContextType.Guild)]
        public async Task Vote([Remainder]string Target)
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if (CurrentGame == null) // Ignore if there is no game running
                return;
            else if (CurrentGame != null && CurrentGame.State == GameState.Signups) // Ignore command if game is not running
                return;
            else if (CurrentGame.State == GameState.Night) // Ignore command if it is night
                return;

            Mafia.Player Voter = null, Votee = null;

            //Fetch the voter and votee from the current game's player list
            foreach(Mafia.Player player in CurrentGame.PlayerList)
            {
                if (player.Username == Context.User)
                    Voter = player;
                if (player.Username.Username == Target || $"<@!{player.Username.Id}>" == Target)
                    Votee = player;
            }

            if (Voter != null && Votee != null) // Ignore the command if either the voter or the votee is not participating in this game
            {
                if (!Votee.Votes.Contains(Voter.Username))
                {
                    Votee.Votes.Add(Voter.Username);

                    await Context.User.SendMessageAsync($"Vote on player {Votee.Username.Username} registered.");
                }
                else
                    await Context.User.SendMessageAsync($"You have already voted on {Votee.Username.Username}!");
            }
            else if (Voter != null && Votee == null)
                await Context.User.SendMessageAsync($"Your vote target \"{Target}\" does not appear to be playing in this game! Please check the spelling and capitalization of your vote target.");
        }

        [Command("unvote")]
        [Summary("Remove your current vote")]
        [RequireContext(ContextType.Guild)]
        public async Task Unvote()
        {
            if (Context.Channel.Name != "mafia") // Ignore command if not typed in mafia channel
                return;

            Game CurrentGame = null;

            foreach (Mafia.Game game in Startup.Games) // Make sure a game is running on this server
            {
                if (game.Server == Context.Guild as SocketGuild)
                    CurrentGame = game;
            }

            if (CurrentGame == null) // Ignore if there is no game running
                return;
            else if (CurrentGame != null && CurrentGame.State == GameState.Signups) // Ignore command if game is not running
                return;
            else if (CurrentGame.State == GameState.Night) // Ignore command if it is night
                return;

            bool Found = false;

            foreach (Mafia.Player player in CurrentGame.PlayerList)
            {
                if (player.Votes.Contains(Context.User))
                {
                    player.Votes.Remove(Context.User);
                    Found = true;

                    await Context.User.SendMessageAsync($"Removed your vote on {player.Username.Username}.");
                }
            }

            if (!Found)
                await Context.User.SendMessageAsync($"You do not appear to have an active vote on any player in the current game on {Context.Guild.Name}!");
        }
    }
}

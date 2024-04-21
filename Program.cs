using System;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace SnookerBot
{
    internal class Program
    {
        static IConfigurationRoot LoadAppSettings()
        {
           IConfigurationRoot appConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            if (string.IsNullOrEmpty(appConfig["IRC_Server"]) ||
                string.IsNullOrEmpty(appConfig["IRC_Port"]) ||
                string.IsNullOrEmpty(appConfig["IRC_User"]) ||
                string.IsNullOrEmpty(appConfig["IRC_Nick"]) ||
                string.IsNullOrEmpty(appConfig["IRC_Channel"]) ||
                string.IsNullOrEmpty(appConfig["Snooker_Season"])
                )
            {
                throw new InvalidOperationException("Missing app settings");
            }

            return appConfig;
        }

        static void Main()
        {
            IConfigurationRoot config = LoadAppSettings();

            var ircBot = new IRCbot(
                server: config["IRC_Server"] ?? "NoServer",
                port: Convert.ToInt32(config["IRC_Port"]),
                user: config["IRC_User"] ?? "NoUser",
                nick: config["IRC_Nick"] ?? "NoNick",
                authUser: config["IRC_AUTH_User"] ?? "NoAuthUser",
                authPass: config["IRC_AUTH_Pass"] ?? "NoAuthPass",
                channel: config["IRC_Channel"] ?? "#NoChannel",
                season: Convert.ToInt32(config["Snooker_Season"])
            );
            _ = ircBot.StartAsync();
        }

        public class IRCbot
        {
            private readonly string _server;
            private readonly int _port;
            private readonly string _user;
            private readonly string _nick;
            private readonly string _channel;
            private readonly string _authUser;
            private readonly string _authPass;
            private readonly int _maxRetries;
            private readonly int _snookerSeason;
            public IRCbot(string server, int port, string user, string nick, string authUser, string authPass, string channel, int season, int maxRetries = 60)
            {
                _server = server;
                _port = port;
                _user = user;
                _nick = nick;
                _channel = channel;
                _authUser = authUser;
                _authPass = authPass;
                _maxRetries = maxRetries;
                _snookerSeason = season;
            }
            public async Task StartAsync()
            {
                var retryCount = 0;
                bool retry;
                do
                {
                    try
                    {
                        using var irc = new TcpClient(_server, _port);
                        using var stream = irc.GetStream();
                        using var reader = new StreamReader(stream);
                        using var writer = new StreamWriter(stream);

                        // ADD for Ident response
                        writer.WriteLine("NICK " + _nick);
                        writer.Flush();
                        writer.WriteLine(_user);
                        writer.Flush();

                        while (true)
                        {
                            string inputLine;
                            while ((inputLine = reader.ReadLine()!) != null)
                            {
                                Console.WriteLine("<- " + inputLine);

                                // split the lines sent from the server by spaces
                                string[] splitInput = inputLine.Split(new Char[] { ' ' });
                                string rawReply = "";
                                string writeToChan = "PRIVMSG " + _channel + " : ";

                                if (splitInput[0] == "PING") { rawReply = "PING"; }             // Server Sent PONG
                                else if (splitInput[1] == "001") { rawReply = "CONNECTED"; }    // Server Sent Connected
                                else if (splitInput[1] == "PRIVMSG") { rawReply = "PRIVMSG"; }  // Server Sent PRIVMSG

                                // rawReply a.k.a reply type from server, defined above, PING to automatically reply with PONG, 
                                // CONNECTED to join defined channel, PRIVMSG to handle !commands
                                switch (rawReply)
                                {
                                    case "PING":
                                        string PongReply = splitInput[1];
                                        writer.WriteLine("PONG " + PongReply);
                                        writer.Flush();
                                        break;
                                    case "CONNECTED":
                                        // AUTH to QuakeNet, set hidden hostmask and wait 2 seconds for mode +x to take effect
                                        if (_authUser != "NoAuthUser" && _authPass != "noAuthPass")
                                        {
                                            writer.WriteLine("AUTH " + _authUser + " " + _authPass);
                                            writer.WriteLine("MODE " + _nick + " +x");
                                            Thread.Sleep(2000);
                                        }

                                        // Join channel(s)
                                        writer.WriteLine("JOIN " + _channel);
                                        writer.Flush();
                                        break;
                                    case "PRIVMSG":
                                        string str;
                                        string[] getNick = inputLine.Split(new Char[] { '!' });
                                        string nick = getNick[0];

                                        // ON HOLD FOR NOW, because lightweight free server, Regexing every line tends to take some CPU
                                        // Regex to check for an alternative !next trigger
                                        // Match regExNextT = Regex.Match(inputLine.Split(
                                        //     new Char[] { ':' })[2], @"\b(when|what)(.*)next(.*)tournament\b", RegexOptions.IgnoreCase
                                        // );
                                        // if (regExNextT.Success)
                                        // {
                                        //     splitInput[3] = ":!next";
                                        // }

                                        // ADMIN commands
                                        if (nick == ":Cail")
                                        {
                                            switch (splitInput[3])
                                            {
                                                case ":!test":
                                                    // Just for testing new stuff

                                                    writer.WriteLine(writeToChan + "Sod off.");
                                                    writer.Flush();
                                                    break;
                                                case ":!nick":
                                                    writer.WriteLine("NICK " + splitInput[4]);
                                                    writer.Flush();
                                                    break;
                                                case ":!exit":
                                                    System.Environment.Exit(1);
                                                    break;
                                            }
                                        }

                                        // USER commands
                                        switch (splitInput[3])
                                        {
                                            case ":!update":
                                                // Update snooker API file to local cache, for faster searching

                                                if (getSnookerInfo.snooker_update(_snookerSeason) != null)
                                                {
                                                    writer.WriteLine(writeToChan + "Cache refreshed.");
                                                    writer.Flush();
                                                }
                                                break;
                                            case ":!upcoming":
                                                // List 5 upcoming tournaments/matches, ignoring the type of tournament

                                                var tournaments = getSnookerInfo.snooker_upcoming();

                                                foreach (var tournament in tournaments)
                                                {
                                                    writer.WriteLine(writeToChan + tournament);
                                                }
                                                writer.Flush();
                                                break;
                                            case ":!next":
                                                // Get the next tournament coming

                                                var nextT = getSnookerInfo.snooker_next();

                                                writer.WriteLine(writeToChan + nextT[1]);
                                                writer.Flush();
                                                break;
                                            case ":!cat":
                                                // Because ofcourse there has to be cats

                                                str = getSnookerInfo.snookerCat();
                                                writer.WriteLine(writeToChan + "Have a random catpic, LOOK AT IT! " + str);
                                                writer.Flush();
                                                break;
                                            case ":!links":
                                                // List links for users, "add" and "remove" commands for selected people
                                                
                                                var linksFile = @"./links.txt";

                                                if (((nick == ":Cail") || (nick == ":Wibble")) && (splitInput.Length > 4))
                                                {
                                                    switch (splitInput[4])
                                                    {
                                                        case "add":
                                                            try
                                                            {
                                                                // Writing to links.txt
                                                                using (StreamWriter sw = File.AppendText(linksFile))
                                                                {
                                                                    sw.WriteLine(string.Join(" ", splitInput[5..]).ToString());
                                                                }

                                                                writer.WriteLine(writeToChan + "Link added");
                                                                writer.Flush();
                                                                break;
                                                            }
                                                            catch (FormatException e)
                                                            {
                                                                // Invalid input for writing
                                                                Console.WriteLine(e.Message);
                                                                writer.WriteLine(writeToChan + "Invalid input");
                                                                writer.Flush();
                                                                break;
                                                            }
                                                            break;
    
                                                        case "remove":
                                                            try
                                                            {
                                                                // Removing a link by link number
                                                                int line_to_delete = Int32.Parse(splitInput[5]);
                                                                Console.WriteLine(line_to_delete);

                                                                string removeReturn = getSnookerInfo.RemoveLineFromFile(linksFile, line_to_delete);

                                                                writer.WriteLine(writeToChan + removeReturn);
                                                                writer.Flush();
                                                                break;
                                                            }
                                                            catch (FormatException e)
                                                            {
                                                                // Invalid range for the link number, or not found
                                                                writer.WriteLine(writeToChan + "Invalid input");
                                                                writer.Flush();
                                                                Console.WriteLine(e.Message);
                                                                break;
                                                            }
                                                            break;
                                                    }
                                                } 
                                                else
                                                {
                                                    // Read lines fron links.txt
                                                    var lines = File.ReadAllLines(linksFile);

                                                    // Essential links, then loop through links.txt
                                                    writer.WriteLine(writeToChan + "Essential links: https://www.wst.tv/ - https://www.snooker.org/ - https://cuetracker.net/");
                                                    writer.Flush();

                                                    for (var i = 0; i < lines.Length; i += 1)
                                                    {
                                                        var line = lines[i];
                                                        writer.WriteLine(writeToChan + (i+1) + ": " + line);
                                                    }
                                                    writer.Flush();
                                                    break;
                                                }

                                                break;
                                            default:
                                                try
                                                {
                                                    if (inputLine.Contains("www") || inputLine.Contains("http"))
                                                    {
                                                        // #SNOOKER CHANNEL'S OWN BLOCK
                                                        if (nick == ":amigo" || inputLine.Contains(".ru")) { break; }

                                                        var response = await getSnookerInfo.get_url_title(inputLine);

                                                        // If we have URL Title(s)
                                                        if (!String.IsNullOrEmpty(response))
                                                        {
                                                            writer.WriteLine(writeToChan + "Title: " + response);
                                                            writer.Flush();
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    // If no response
                                                    Console.WriteLine(e.ToString());
                                                    writer.WriteLine(writeToChan + "Title: Server sent bad reply.");
                                                    writer.Flush();
                                                }
                                                break;
                                        }

                                        // END of rawReply Switch
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        Thread.Sleep(5000);
                        retry = ++retryCount <= _maxRetries;
                    }
                } while (retry);
            }
        }
    }
}


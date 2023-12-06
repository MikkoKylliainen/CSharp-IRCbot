using System;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;


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
                channel: config["IRC_Channel"] ?? "#NoChannel",
                season: Convert.ToInt32(config["Snooker_Season"])
            );

            Task task = ircBot.StartAsync();
        }

        public class IRCbot
        {
            private readonly string _server;
            private readonly int _port;
            private readonly string _user;
            private readonly string _nick;
            private readonly string _channel;
            private readonly int _maxRetries;
            private readonly int _snookerSeason;
            public IRCbot(string server, int port, string user, string nick, string channel, int season, int maxRetries = 60)
            {
                _server = server;
                _port = port;
                _user = user;
                _nick = nick;
                _channel = channel;
                _maxRetries = maxRetries;
                _snookerSeason = season;
            }
            public async Task StartAsync()
            {
                var retry = false;
                var retryCount = 0;
                do
                {
                    try
                    {
                        using (var irc = new TcpClient(_server, _port))
                        using (var stream = irc.GetStream())
                        using (var reader = new StreamReader(stream))
                        using (var writer = new StreamWriter(stream))
                        {
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
                                            writer.WriteLine("JOIN " + _channel);
                                            writer.Flush();
                                            break;
                                        case "PRIVMSG":
                                            string str;
                                            string[] getNick = inputLine.Split(new Char[] { '!' });
                                            string nick = getNick[0];

                                            // Regex to check for an alternative !next trigger
                                            Match regExNextT = Regex.Match(inputLine.Split(
                                                new Char[] { ':' })[2], @"\bwhen(.*)next(.*)tournament\b", RegexOptions.IgnoreCase
                                            );
                                            if (regExNextT.Success)
                                            {
                                                splitInput[3] = ":!next";
                                            }

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
                                                    if (getSnookerInfo.snooker_update(_snookerSeason) != null)
                                                    {
                                                        writer.WriteLine(writeToChan + "Cache refreshed.");
                                                        writer.Flush();
                                                    }
                                                    break;
                                                case ":!upcoming":
                                                    var tournaments = getSnookerInfo.snooker_upcoming();

                                                    foreach (var tournament in tournaments)
                                                    {
                                                        writer.WriteLine(writeToChan + tournament);
                                                    }
                                                    writer.Flush();
                                                    break;
                                                case ":!next":
                                                    var nextT = getSnookerInfo.snooker_next();

                                                    writer.WriteLine(writeToChan + nextT[1]);
                                                    writer.Flush();
                                                    break;
                                                case ":!cat":
                                                    str = getSnookerInfo.snookerCat();
                                                    writer.WriteLine(writeToChan + "Have a random catpic, LOOK AT IT! " + str);
                                                    writer.Flush();
                                                    break;
                                                default:
                                                    try
                                                    {
                                                        var response = await getSnookerInfo.get_url_title(inputLine);

                                                        // If we have URL Title(s)
                                                        if (!String.IsNullOrEmpty(response))
                                                        {
                                                            writer.WriteLine(writeToChan + "Title: " + response);
                                                            writer.Flush();
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


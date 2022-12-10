
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace SnookerBot
{
    internal class Program
    {
        static void Main()
        {
            var ircBot = new IRCbot(
                server: "irc.quakenet.org",
                port: 6667,
                user: "USER SnookerBot 0 * :SnookerBot",
                nick: "MarkSelby",
                channel: "#huuhaa"
            );

            ircBot.Start();
        }

        public class IRCbot
        {
            private readonly string _server;
            private readonly int _port;
            private readonly string _user;
            private readonly string _nick;
            private readonly string _channel;
            private readonly int _maxRetries;
            public IRCbot(string server, int port, string user, string nick, string channel, int maxRetries = 3)
            {
                _server = server;
                _port = port;
                _user = user;
                _nick = nick;
                _channel = channel;
                _maxRetries = maxRetries;
            }
            public void Start()
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
                                while ((inputLine = reader.ReadLine()) != null)
                                {
                                    Console.WriteLine("<- " + inputLine);

                                    // split the lines sent from the server by spaces
                                    string[] splitInput = inputLine.Split(new Char[] { ' ' });
                                    string rawReply = "";
                                    string writeToChan = "PRIVMSG " + _channel + " : ";

                                    if (splitInput[0] == "PING") { rawReply = "PING";  }            // Server Sent PONG
                                    else if (splitInput[1] == "001") { rawReply = "CONNECTED"; }    // Server Sent Connected
                                    else if (splitInput[1] == "PRIVMSG") { rawReply = "PRIVMSG"; }  // Server Sent PRIVMSG

                                    // rawReply a.k.a reply type from server, defined above, PING to automatically reply with PONG, CONNECTED to join defined channel, PRIVMSG to handle !commands
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
                                            Match regExNextT = Regex.Match(inputLine.Split(new Char[] { ':' })[2], @"\bwhen(.*)next(.*)tournament\b", RegexOptions.IgnoreCase);
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
                                                        writer.WriteLine(writeToChan + "Testing");
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
                                                    if (getSnookerInfo.snooker_update() != null)
                                                    {
                                                        writer.WriteLine("PRIVMSG " + _channel + " :Cache refreshed.");
                                                        writer.Flush();
                                                    }
                                                    break;
                                                case ":!upcoming":
                                                    var tournaments = getSnookerInfo.snooker_upcoming();

                                                    foreach (var tournament in tournaments)
                                                    {
                                                        writer.WriteLine("PRIVMSG " + _channel + " :" + tournament);
                                                    }
                                                    writer.Flush();
                                                    break;
                                                case ":!next":
                                                    var nextT = getSnookerInfo.snooker_next();

                                                    writer.WriteLine("PRIVMSG " + _channel + " :" + nextT[1]);
                                                    writer.Flush();
                                                    break;
                                                case ":!cat":
                                                    str = getSnookerInfo.snooker_cat();
                                                    writer.WriteLine("PRIVMSG " + _channel + " :Have a random catpic, LOOK AT IT! " + str);
                                                    writer.Flush();
                                                    break;
                                                default:
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


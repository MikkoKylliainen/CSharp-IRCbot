
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
                nick: "JuddTrump",
                channel: "#snooker"
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

                                    if (splitInput[0] == "PING")
                                    {
                                        string PongReply = splitInput[1];
                                        //Console.WriteLine("->PONG " + PongReply);
                                        writer.WriteLine("PONG " + PongReply);
                                        writer.Flush();
                                        //continue;
                                    }
                                    else if (splitInput[1] == "001")
                                    {
                                        writer.WriteLine("JOIN " + _channel);
                                        writer.Flush();
                                    }

                                    else if (splitInput[1] == "PRIVMSG")
                                    {
                                        string str;

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
                                                writer.WriteLine("PRIVMSG " + _channel + " :" + str);
                                                writer.Flush();
                                                break;
                                            default:
                                                break;
                                        }
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


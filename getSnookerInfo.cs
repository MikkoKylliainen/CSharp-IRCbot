using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SnookerBot
{
    public class getSnookerInfo
    {
        // Update function to refresh "cached" API call
        public static async Task<string> snooker_update(int Snooker_Season = 2023)
        {
            var data = Task.Run(() => GetDataFromAPI("http://api.snooker.org/?t=5&s=" + Snooker_Season));
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
            data.Wait();

            await File.WriteAllTextAsync("./snooker_schedule.txt", data.Result);
            return "1";
        }

        // List maximum of 5 upcoming tournaments, regardless of tournament type
        public static List<string> snooker_upcoming()
        {
            string snookerInfo = File.ReadAllText(@"./snooker_schedule.txt");

            dynamic? getTournaments = JsonConvert.DeserializeObject(snookerInfo);
            List<string> tournaments = new List<string>();
            var x = 1;

            foreach (var tournament in getTournaments ?? Enumerable.Empty<int>())
            {
                var endDate = DateTime.Parse(tournament.EndDate.ToString());
                if (endDate > DateTime.Now)
                {
                    string tStartDate = tournament.StartDate;
                    string tEndDate = tournament.EndDate;

                    tournaments.Add(x + ": " + tournament.Name + " | " + ModDate(tStartDate, tEndDate) + " | Type: " + tournament.Type);

                    x++;
                }
                if (x == 6) { break; }
            }

            return tournaments;
        }

        public static List<string> snooker_next()
        {
            try {
                string snookerInfo = File.ReadAllText(@"./snooker_schedule.txt");

                dynamic? getTournaments = JsonConvert.DeserializeObject(snookerInfo);
                List<string> tournaments = new List<string>();

                string tCurrent = "None";
                string tNext = "None";

                foreach (var tournament in getTournaments ?? Enumerable.Empty<int>())
                {
                    string tName = tournament.Name;
                    string tType = tournament.Type;
                    string tStartDate = tournament.StartDate;
                    string tEndDate = tournament.EndDate;

                    // only select ranking tournaments and invitationals in the list
                    string[] invitationals = { "Masters", "World Masters", "Shanghai Masters", "Champion of Champions", "Paul Hunter Classic" };
                    if (tType == "Ranking" || Array.Find(invitationals, element => element == tName) != null)
                    {
                        var StartT = DateTime.Parse(tStartDate);
                        var EndT = DateTime.Parse(tEndDate);

                        // Current Tournament
                        if (StartT <= DateTime.Now && EndT.AddSeconds(76400) > DateTime.Now)
                        {
                            tCurrent = tName + " " + ModDate(tStartDate, tEndDate);
                        }

                        // Next Tournament
                        if (StartT > DateTime.Now)
                        {
                            tNext = "Next is: " + ModDate(tStartDate, tEndDate) + " | " + tName;
                            break;
                        }
                    }
                }

                List<string> arraytNext = new List<string>();
                arraytNext.Add(tCurrent);
                arraytNext.Add(tNext);
                return arraytNext;
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
                List<string> arraytNext = new List<string>();
                arraytNext.Add("Error");
                return arraytNext;
            }
        }

        public static string RemoveLineFromFile(string filePath, int lineNumberToRemove)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lineNumberToRemove < 1 || lineNumberToRemove > lines.Length)
            {
                Console.WriteLine("Invalid line number.");
                return "Invalid link number";
            }

            // Create a new array to hold the lines without the one to be removed
            string[] updatedLines = new string[lines.Length - 1];
            int index = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (i != lineNumberToRemove - 1) // Line numbers are 1-based
                {
                    updatedLines[index] = lines[i];
                    index++;
                }
            }

            // Write the updated lines back to the file
            File.WriteAllLines(filePath, updatedLines);

            return "Link removed";
        }

        public static string snookerCat()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
            var data = Task.Run(() => GetDataFromAPI("https://api.thecatapi.com/v1/images/search"));
            data.Wait(); 

            var jsonResult = data.Result.TrimStart(new char[] { '[' }).TrimEnd(new char[] { ']' });
            JObject j = JObject.Parse(jsonResult);
            string catURL = j["url"]!.ToString();

            return catURL;
        }

        public static async Task<string> get_url_title(string title)
        {
            var returnInfo = "";
            foreach (Match item in Regex.Matches(title, @"(https?):\/\/([\w\-_]+(?:(?:\.[\w\-_]+)+))([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?"))
            {
                try
                {
                    var data = Task.Run(() => GetDataFromAPI(item.Value));
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
                    data.Wait();

                    // Regex find the Title
                    string response = Regex.Match(data.Result, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>", RegexOptions.IgnoreCase).Groups["Title"].Value;

                    // Decode HTML characters to text
                    response = System.Net.WebUtility.HtmlDecode(response);

                    // ALL MATCHED URL TITLES
                    if (!String.IsNullOrEmpty(response))
                    {
                        // RETURN FIRST ONE FOR NOW
                        return response;
                        break;
                    }
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\nException Caught!");
                    Console.WriteLine("Message :{0} ", e.Message);
                }
            }
            return null;
        }

        public static string ModDate(string StartDate, string EndDate)
        {
            var StartT = DateTime.Parse(StartDate);
            var EndT = DateTime.Parse(EndDate);

            if (StartT.Month == EndT.Month)
            {
                var returnDates = StartT.Day + "-" + EndT.Day + " " + EndT.ToString("MMM");
                return returnDates;
            }
            else
            {
                var returnDates = StartT.ToString("d MMM") + " - " + EndT.ToString("d MMM");
                return returnDates;
            }
        }

        static async Task<string> GetDataFromAPI(string url)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Requested-By", "MikkoBot");
            HttpResponseMessage result = await client.GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();

            return response;
        }
    }
} 

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Transactions;
using static System.Net.Mime.MediaTypeNames;

namespace SnookerBot
{
    public class getSnookerInfo 
    {
        public static async Task<string> snooker_update() {
            var data = Task.Run(() => GetDataFromAPI("http://api.snooker.org/?t=5&s=2022"));
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
            data.Wait();

            await File.WriteAllTextAsync("./snooker_schedule.txt", data.Result);
            return "1";
        }
        public static List<string> snooker_upcoming() {
            string snookerInfo = File.ReadAllText(@"./snooker_schedule.txt");

            dynamic getTournaments = JsonConvert.DeserializeObject(snookerInfo);
            List<string> tournaments = new List<string>();
            var x = 1;

            foreach (var tournament in getTournaments)
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
            string snookerInfo = File.ReadAllText(@"./snooker_schedule.txt");

            dynamic getTournaments = JsonConvert.DeserializeObject(snookerInfo);
            List<string> tournaments = new List<string>();

            string tCurrent = "None";
            string tNext = "None";

            foreach (var tournament in getTournaments)
            {
                string tName = tournament.Name;
                string tType = tournament.Type;
                string tStartDate = tournament.StartDate;
                string tEndDate = tournament.EndDate;

                // only select ranking tournaments and invitationals in the list
                string[] invitationals = { "Masters", "Shanghai Masters", "Champion of Champions", "Paul Hunter Classic" };
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
        public static string snooker_cat()
        {
            var data = Task.Run(() => GetDataFromAPI("https://api.thecatapi.com/v1/images/search"));
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
            data.Wait();

            var jsonResult = data.Result.TrimStart(new char[] { '[' }).TrimEnd(new char[] { ']' });
            JObject j = JObject.Parse(jsonResult);
            string catURL = j["url"].ToString();

            return catURL;
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
            HttpResponseMessage result = await client.GetAsync(url);
            var response = await result.Content.ReadAsStringAsync();

            return response;
        }
    }
}

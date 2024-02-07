using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Configuration;

namespace Crawling
{
    internal class AuctionDataProcessor
    {
        public async Task<List<AuctionData>> ExtractAuctionDataAsync(string url)
        {
            List<AuctionData> auctionDataList = new List<AuctionData>();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string htmlContent = await client.GetStringAsync(url);
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(htmlContent);

                    var titles = doc.DocumentNode.SelectNodes("//*[@class='auction-item__name']")
                        ?.Select(node => node.InnerText.Trim()) ?? Enumerable.Empty<string>();

                    var links = doc.DocumentNode.SelectNodes("//*[@class='auction-item__btns']/a")
                        ?.Select(node => node.GetAttributeValue("href", null)) ?? Enumerable.Empty<string>();

                    var imageUrls = doc.DocumentNode.SelectNodes("//*[@class='auction-item__image']/img[@src]")
                        ?.Select(node => node.GetAttributeValue("src", null).Trim()) ?? Enumerable.Empty<string>();

                    var lots = doc.DocumentNode.SelectNodes("//*[@class='auction-item__btns']")
                        ?.Select(node => node.InnerText.Trim()) ?? Enumerable.Empty<string>();

                    var locations = doc.DocumentNode.SelectNodes("//div [@class='auction-date-location__item'][2]//span")
                        ?.Select(node => node.InnerText.Trim()) ?? Enumerable.Empty<string>();

                    var dptions = doc.DocumentNode.SelectNodes("//div[@class='auction-date-location']")
                        ?.Select(node =>
                        {
                            string part1 = node.SelectSingleNode("div[@class='auction-date-location__item'][1]")?.InnerText.Trim() ?? "";               // fetchs Time
                            string part2 = node.SelectSingleNode("div[@class='auction-date-location__item'][2]/span")?.InnerText.Trim() ?? "";          // fetches Location

                            return string.Join(" , ", part1, part2);                                                                                    // Joining Time , Location
                        }) ?? Enumerable.Empty<string>();

                    var Date = doc.DocumentNode.SelectNodes("//div[@class='auction-date-location__item'][1]")
                        ?.Select(node => node.InnerText.Trim()) ?? Enumerable.Empty<string>();

                    if (titles.Count() != links.Count() || titles.Count() != imageUrls.Count() || titles.Count() != lots.Count() || titles.Count() != locations.Count() || titles.Count() != dptions.Count() || titles.Count() != Date.Count())
                    {
                        Console.WriteLine("Mismatch in the number of titles, image URLs, and lot.");
                        return auctionDataList;
                    }

                    for (int i = 0; i < titles.Count(); i++)
                    {
                        auctionDataList.Add(new AuctionData
                        {
                            Title = titles.ElementAt(i).Trim(),
                            Link = links.ElementAt(i).Trim(),
                            ImageUrl = imageUrls.ElementAt(i).Trim(),
                            Lot = ExtractNumericLot(lots.ElementAt(i)).Trim(),
                            Location = FormatLocation(locations.ElementAt(i)).Trim(),
                            Description = dptions.ElementAt(i).Trim(),
                            StartDate = ExtractStartDate(Date.ElementAt(i)).Trim(),
                            StartMonth = ExtractStartMonth(Date.ElementAt(i)).Trim(),
                            StartYear = ExtractStartYear(Date.ElementAt(i)).Trim(),
                            StartTime = ExtractStartTime(Date.ElementAt(i)).Trim(),
                            EndDate = ExtractEndDate(Date.ElementAt(i)).Trim(),
                            EndMonth = ExtractEndMonth(Date.ElementAt(i)).Trim(),
                            EndYear = ExtractEndYear(Date.ElementAt(i)).Trim(),
                            EndTime = ExtractEndTime(Date.ElementAt(i).Trim())
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting auction data: {ex.Message}");
            }

            return auctionDataList;
        }



        /**
         * Method : To Insert Data in Data-Base
         */
        public void InsertAuctionDataIntoDatabase(List<AuctionData> auctionDataList)
        {
            string conn = ConfigurationManager.ConnectionStrings["CreateConnection"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(conn))
            {
                connection.Open();

                try
                {
                    foreach (var data in auctionDataList)
                    {
                        string insertQuery = "INSERT INTO Auctions (Title, Description, ImageUrl, Link, LotCount, StartDate, StartMonth, StartYear, StartTime, EndDate, EndMonth, EndYear, EndTime, Location) " +
                                             "VALUES (@Title, @Description, @ImageUrl, @Link, @LotCount, @StartDate, @StartMonth, @StartYear , @StartTime , @EndDate, @EndMonth, @EndYear, @EndTime , @Location)";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Title", data.Title);
                            command.Parameters.AddWithValue("@Description", data.Description);
                            command.Parameters.AddWithValue("@ImageUrl", data.ImageUrl);
                            command.Parameters.AddWithValue("@Link", data.Link);
                            command.Parameters.AddWithValue("@LotCount", data.Lot);
                            command.Parameters.AddWithValue("@StartDate", data.StartDate);
                            command.Parameters.AddWithValue("@StartMonth", data.StartMonth);
                            command.Parameters.AddWithValue("@StartYear", data.StartYear);
                            command.Parameters.AddWithValue("@StartTime", data.StartTime);
                            command.Parameters.AddWithValue("@EndDate", data.EndDate);
                            command.Parameters.AddWithValue("@EndMonth", data.EndMonth);
                            command.Parameters.AddWithValue("@EndYear", data.EndYear);
                            command.Parameters.AddWithValue("@EndTime", data.EndTime);
                            command.Parameters.AddWithValue("@Location", data.Location);

                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message.ToString());
                }
            }
        }



        /**
         * Method : To Extract Lot No.
         */
        private static string ExtractNumericLot(string lotText)
        {

            Match match = Regex.Match(lotText, @"\b(\d+)\b");                           // extract numeric value

            if (match.Success)
            {
                return match.Value;
            }

            return "null";
        }
        


        /**
         * Method : To Extract Location
         */
        private static string FormatLocation(string location)                                // Timed Auction, Zürich
        {
            int indexOfAuctionPipe = location.IndexOf("Auction |", StringComparison.OrdinalIgnoreCase);
            int indexOfAuctionComma = location.IndexOf("Auction,", StringComparison.OrdinalIgnoreCase);

            if (indexOfAuctionPipe != -1)
            {
                return location.Substring(indexOfAuctionPipe + "Auction |".Length).Trim();
            }
            else if (indexOfAuctionComma != -1)
            {
                return location.Substring(indexOfAuctionComma + "Auction,".Length).Trim();  // indexOfAuctionComma = 6 , "Auction,".Length = 8  ( 6 + 8 = 14 ) it will start substring from index 14 till last.
            }

            return location;
        }



        /**
         * Method : To Extract Start Date
         */
        private static string ExtractStartDate(string date)
        {
            Match match = Regex.Match(date, @"\b\d+");            // extract start date     
                                                                  // another = (?<abc>(?<StartDate>\d{1,2})( )?(-)?( )?(\d{1,2})?([A-Z]{3,})?( )?(,)?(-)?( )?([A-Z]+)?( )?(\d{4})?( )?(\d+:\d+)?( )?(\()?(CET)?(\))?)( )?(-)?( )?((\d{1,2})( )?(-)?( )?(\d{1,2})?([A-Z]{3,})?( )?(,)?(-)?( )?([A-Z]+)?( )?(\d{4})?( )?(\d+:\d+)?( )?(\()?(CET)?(\))?)?((,)?( )?(\d+:\d+)?( )?(\()?(\w+)?(\)))?
            if (match.Success)
            {
                return match.Value;
            }

            return "null";
        }



        /**
         * Method : To Extract Start Month
         */

        private static string ExtractStartMonth(string date)
        {
            Match match = Regex.Match(date, @"(\w{3,})");          // extract start month

            if (match.Success)
            {
                string month = match.Value.Substring(0, 3);
                switch (month)
                {
                    case "JAN":
                        return "01";
                    case "FEB":
                        return "02";
                    case "MAR":
                        return "03";
                    case "APR":
                        return "04";
                    case "MAY":
                        return "05";
                    case "JUN":
                        return "06";
                    case "JUL":
                        return "07";
                    case "AUG":
                        return "08";
                    case "SEP":
                        return "09";
                    case "OCT":
                        return "10";
                    case "NOV":
                        return "11";
                    case "DEC":
                        return "12";
                    default:
                        return "null";
                }
            }

            return "null";
        }



        /**
         * Method : To Extract Start Year
         */

        private static string ExtractStartYear(string date)
        {
            Match match = Regex.Match(date, @"(\d{4,})");            // extract start year

            if (match.Success)
            {
                return match.Value;
            }

            return "null";
        }



        /**
         * Method : To Extract Start Time
         */

        private static string ExtractStartTime(string date)
        {
            Match match = Regex.Match(date, @"(\d{2}:\d{2})");            // extract start Time         another regex = (\d+:\d+)

            if (match.Success)
            {
                return match.Value;
            }

            return "null";
        }



        /**
         * Method : To Extract End Date
         */

        private static string ExtractEndDate(string date)
        {
            Match match1 = Regex.Match(date, @"-\s*(\d{1,2})");
            Match match2 = Regex.Match(date, @"((\d+)?( )?([A-Z]{3,})?(, )?(\d{2})?(\:)?(\d{2})?( )?([A-Z]{3})?)(( )?(?<EndDate>\d+)?( )?([A-Z]{3,})?(, )?(\d{2})?(\:)?(\d{2})?( )?([A-Z]+)?)?");

            if (match1.Groups[1].Value.Length <= 2 && match1.Groups[1].Value.Length >= 1)
            {
                return match1.Groups[1].Value;
            }

            else if (match2.Groups["EndDate"].Value.Length <= 2 && match2.Groups["EndDate"].Value.Length >= 1)
            {
                return match2.Groups["EndDate"].Value;
            }

            return "null";

        }



        /**
         * Method : To Extract End Month
         */

        private static string ExtractEndMonth(string date)
        {
            Match match1 = Regex.Match(date, @"(\d+)?( )?([A-Z]{3,})?(, )?(\d{2})?(\:)?(\d{2})?( )?([A-Z]{3})?(-)?( )?(\d+)?( )?(-)?(\d+)?( )?(\d+)?(?<EndMonth>[A-Z]{3,})?(, )?(\d+)?(\:)?(\d{2})?( )?([A-Z]+)?( )?(\d+)?|((\d+ )?([J,F,M,A,S,O,N,D][A-Z]+)?( )?(\d{4}))?( )?(-)?( )?((\d+ )?(?<EndMonth>[J,F,M,A,S,O,N,D][A-Z]+)?( )?(\d{4}))?");
            Match match2 = Regex.Match(date, @"((\d+ )?([J,F,M,A,S,O,N,D][A-Z]+)?( )?(\d{4}))?( )?(-)?( )?((\d+ )?(?<EndMonth>[J,F,M,A,S,O,N,D][A-Z]+)?( )?(\d{4}))?");

            if (match1.Success || match2.Success)
            {
                string match = "";

                if (match1.Groups["EndMonth"].Value.Length >= 3)
                {
                    match = match1.Groups["EndMonth"].Value;
                }

                else if (match2.Groups["EndMonth"].Value.Length >= 3)
                {
                    match = match2.Groups["EndMonth"].Value;
                }

                else
                {
                    return "null";
                }

                string month = match.Substring(0, 3);

                switch (month)
                {
                    case "JAN":
                        return "01";
                    case "FEB":
                        return "02";
                    case "MAR":
                        return "03";
                    case "APR":
                        return "04";
                    case "MAY":
                        return "05";
                    case "JUN":
                        return "06";
                    case "JUL":
                        return "07";
                    case "AUG":
                        return "08";
                    case "SEP":
                        return "09";
                    case "OCT":
                        return "10";
                    case "NOV":
                        return "11";
                    case "DEC":
                        return "12";
                    default:
                        return "null";
                }
            }

            return "null";
        }



        /**
         * Method : To Extract End Year
         */

        private static string ExtractEndYear(string date)
        {
            Match match = Regex.Match(date, @"((\d+ )?([A-Z]+)?(,)?( )?(\d+)?(\:)?(\d+)?( )?(\w*)?( )?)?(-( )?\d+ ([A-Z]*))?( )?(?<EndYear>\d{4})?");            // extract start Time         another regex = (\d+:\d+)


            if (match.Groups["EndYear"].Value.Length == 4)
            {
                return match.Groups["EndYear"].Value;
            }

            return "null";
        }



        /**
         * Method : To Extract End Time
         */

        private static string ExtractEndTime(string date)
        {
            Match match = Regex.Match(date, @"(\d+)( )([\w]+)(\,)( )(\d{2}:\d{2})([\w\,\ ]+)(?<EndTime>\d{2}:\d{2})+([\w\,\ ]+)?");            // extract End Time

            if (match.Success)
            {
                return match.Groups["EndTime"].Value;
            }

            return "null";

        }
    }
}

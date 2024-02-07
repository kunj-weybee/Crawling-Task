// Program.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Crawling
{
    internal class Program
    {
        public static async Task Main()
        {
            string url = "https://ineichen.com/auctions/past/";

            AuctionDataProcessor auctionDataProcessor = new AuctionDataProcessor();
            List<AuctionData> auctionDataList = await auctionDataProcessor.ExtractAuctionDataAsync(url);

            if (auctionDataList == null)
            {
                Console.WriteLine("Mismatch in the number of titles, image URLs, and lot.");
                return;
            }

            auctionDataProcessor.InsertAuctionDataIntoDatabase(auctionDataList);

            Console.WriteLine("Successfully Crawled "+ auctionDataList.Count + " item.");
            Console.ReadLine();
        }
    }
}

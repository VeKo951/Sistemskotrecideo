using projekat3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace projekat3.Services
{
    internal class CoinGeckoService
    {
        private readonly HttpClient httpClient;
        private readonly Logger logger;

        private const string BaseUrl = "https://api.coingecko.com/api/v3";

        public CoinGeckoService(Logger logger)
        {
            this.logger = logger;

            httpClient = new HttpClient();

            // CoinGecko preporucuje da zahtev ima User-Agent.
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoTrendAnalyzer/1.0");
            httpClient.Timeout = TimeSpan.FromSeconds(20);
        }

        public async Task<List<CryptoCoin>> GetTrendingCoinsAsync()
        {
            logger.Log("CoinGeckoService: zapocinje preuzimanje trendujucih kriptovaluta.");

            List<string> trendingIds = await GetTrendingCoinIdsAsync();

            if (trendingIds.Count == 0)
            {
                logger.Log("CoinGeckoService: CoinGecko nije vratio nijednu trendujucu kriptovalutu.");
                return new List<CryptoCoin>();
            }

            List<CryptoCoin> coinsWithMarketData = await GetMarketDataAsync(trendingIds);

            logger.Log("CoinGeckoService: uspesno preuzeti podaci. Broj kriptovaluta: " + coinsWithMarketData.Count);

            return coinsWithMarketData;
        }

        private async Task<List<string>> GetTrendingCoinIdsAsync()
        {
            string url = BaseUrl + "/search/trending";

            string json = await httpClient.GetStringAsync(url);

            List<string> coinIds = new List<string>();

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("coins", out JsonElement coinsElement))
                {
                    return coinIds;
                }

                foreach (JsonElement coinElement in coinsElement.EnumerateArray())
                {
                    if (!coinElement.TryGetProperty("item", out JsonElement itemElement))
                    {
                        continue;
                    }

                    if (!itemElement.TryGetProperty("id", out JsonElement idElement))
                    {
                        continue;
                    }

                    string? id = idElement.GetString();

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        coinIds.Add(id);
                    }
                }
            }

            // Uzimamo prvih 10 da ne saljemo previse podataka i da projekat bude pregledan.
            return coinIds
                .Distinct()
                .Take(10)
                .ToList();
        }

        private async Task<List<CryptoCoin>> GetMarketDataAsync(List<string> coinIds)
        {
            string ids = string.Join(",", coinIds);

            string url =
                BaseUrl +
                "/coins/markets?vs_currency=usd" +
                "&ids=" + Uri.EscapeDataString(ids) +
                "&price_change_percentage=24h";

            string json = await httpClient.GetStringAsync(url);

            List<CryptoCoin> coins = new List<CryptoCoin>();

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;

                foreach (JsonElement coinElement in root.EnumerateArray())
                {
                    CryptoCoin coin = new CryptoCoin();

                    if (coinElement.TryGetProperty("id", out JsonElement idElement))
                    {
                        coin.Id = idElement.GetString() ?? string.Empty;
                    }

                    if (coinElement.TryGetProperty("symbol", out JsonElement symbolElement))
                    {
                        coin.Symbol = (symbolElement.GetString() ?? string.Empty).ToUpper();
                    }

                    if (coinElement.TryGetProperty("name", out JsonElement nameElement))
                    {
                        coin.Name = nameElement.GetString() ?? string.Empty;
                    }

                    if (coinElement.TryGetProperty("current_price", out JsonElement priceElement)
                        && priceElement.ValueKind != JsonValueKind.Null)
                    {
                        coin.CurrentPrice = priceElement.GetDecimal();
                    }

                    if (coinElement.TryGetProperty("price_change_percentage_24h", out JsonElement changeElement)
                        && changeElement.ValueKind != JsonValueKind.Null)
                    {
                        coin.PriceChangePercentage24h = changeElement.GetDouble();
                    }

                    coins.Add(coin);
                }
            }

            return coins
                .OrderByDescending(c => c.PriceChangePercentage24h)
                .ToList();
        }
    }
}
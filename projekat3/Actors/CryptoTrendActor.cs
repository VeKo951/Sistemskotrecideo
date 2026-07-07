using Akka.Actor;
using projekat3.Messages;
using projekat3.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace projekat3.Actors
{
    internal class CryptoTrendActor : ReceiveActor
    {
        private List<CryptoCoin> currentCoins = new List<CryptoCoin>();

        private DateTime lastUpdated = DateTime.MinValue;

        private int successfulUpdates = 0;

        private int failedUpdates = 0;

        private string lastError = string.Empty;

        private readonly Logger logger;

        public CryptoTrendActor(Logger logger)
        {
            this.logger = logger;

            Receive<UpdateCryptoDataMessage>(message =>
            {
                HandleUpdateCryptoData(message);
            });

            Receive<UpdateFailedMessage>(message =>
            {
                HandleUpdateFailed(message);
            });

            Receive<GetTrendingMessage>(_ =>
            {
                Sender.Tell(currentCoins);
            });

            Receive<GetGroupedMessage>(_ =>
            {
                var groupedCoins = GroupCoinsByChange();
                Sender.Tell(groupedCoins);
            });

            Receive<GetStatusMessage>(_ =>
            {
                CryptoStatusMessage status = new CryptoStatusMessage
                {
                    LastUpdated = lastUpdated,
                    CoinCount = currentCoins.Count,
                    SuccessfulUpdates = successfulUpdates,
                    FailedUpdates = failedUpdates,
                    LastError = lastError
                };

                Sender.Tell(status);
            });
        }

        private void HandleUpdateCryptoData(UpdateCryptoDataMessage message)
        {
            List<CryptoCoin> updatedCoins = new List<CryptoCoin>();

            foreach (CryptoCoin coin in message.Coins)
            {
                coin.Group = GetGroupName(coin.PriceChangePercentage24h);
                updatedCoins.Add(coin);
            }

            currentCoins = updatedCoins
                .OrderByDescending(c => c.PriceChangePercentage24h)
                .ToList();

            lastUpdated = DateTime.Now;
            successfulUpdates++;
            lastError = string.Empty;

            logger.Log("CryptoTrendActor: podaci azurirani. Broj kriptovaluta: " + currentCoins.Count);
        }

        private void HandleUpdateFailed(UpdateFailedMessage message)
        {
            failedUpdates++;
            lastError = message.ErrorMessage;

            logger.Log("CryptoTrendActor: greska pri azuriranju podataka: " + message.ErrorMessage);
        }

        private Dictionary<string, List<CryptoCoin>> GroupCoinsByChange()
        {
            Dictionary<string, List<CryptoCoin>> groups = currentCoins
                .GroupBy(c => c.Group)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(c => c.PriceChangePercentage24h).ToList()
                );

            return groups;
        }

        private string GetGroupName(double changePercentage)
        {
            if (changePercentage >= 5)
            {
                return "Jako rastu";
            }

            if (changePercentage > 1)
            {
                return "Blago rastu";
            }

            if (changePercentage >= -1)
            {
                return "Stabilne";
            }

            if (changePercentage > -5)
            {
                return "Blago padaju";
            }

            return "Jako padaju";
        }
    }
}
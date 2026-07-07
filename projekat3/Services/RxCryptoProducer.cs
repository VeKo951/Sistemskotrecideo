using Akka.Actor;
using projekat3.Messages;
using projekat3.Models;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace projekat3.Services
{
    internal class RxCryptoProducer : IDisposable
    {
        private readonly CoinGeckoService coinGeckoService;
        private readonly IActorRef cryptoActor;
        private readonly Logger logger;
        private readonly TimeSpan refreshInterval;

        private IDisposable? subscription;

        public RxCryptoProducer(
            CoinGeckoService coinGeckoService,
            IActorRef cryptoActor,
            Logger logger,
            TimeSpan refreshInterval)
        {
            this.coinGeckoService = coinGeckoService;
            this.cryptoActor = cryptoActor;
            this.logger = logger;
            this.refreshInterval = refreshInterval;
        }

        public void Start()
        {
            logger.Log("RxCryptoProducer: pokretanje Rx toka podataka.");

            subscription = Observable
                .Timer(TimeSpan.Zero, refreshInterval, TaskPoolScheduler.Default)
                .SelectMany(_ => Observable.FromAsync(FetchCryptoDataAsync))
                .Subscribe(
                    result =>
                    {
                        if (result.Success)
                        {
                            logger.Log("RxCryptoProducer: podaci uspesno preuzeti, salje se poruka aktoru.");

                            cryptoActor.Tell(new UpdateCryptoDataMessage(result.Coins));
                        }
                        else
                        {
                            logger.Log("RxCryptoProducer: greska pri preuzimanju podataka: " + result.ErrorMessage);

                            cryptoActor.Tell(new UpdateFailedMessage(result.ErrorMessage));
                        }
                    },
                    error =>
                    {
                        logger.Log("RxCryptoProducer: neocekivana greska u Rx toku: " + error.Message);

                        cryptoActor.Tell(new UpdateFailedMessage(error.Message));
                    });
        }

        private async System.Threading.Tasks.Task<FetchResult> FetchCryptoDataAsync()
        {
            try
            {
                logger.Log("RxCryptoProducer: periodican zahtev ka CoinGecko API-ju.");

                List<CryptoCoin> coins = await coinGeckoService.GetTrendingCoinsAsync();

                if (coins.Count == 0)
                {
                    return FetchResult.Failed("CoinGecko API je vratio praznu listu kriptovaluta.");
                }

                return FetchResult.Successful(coins);
            }
            catch (Exception ex)
            {
                return FetchResult.Failed(ex.Message);
            }
        }

        public void Stop()
        {
            if (subscription != null)
            {
                subscription.Dispose();
                subscription = null;

                logger.Log("RxCryptoProducer: Rx tok podataka je zaustavljen.");
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private class FetchResult
        {
            public bool Success { get; private set; }

            public List<CryptoCoin> Coins { get; private set; } = new List<CryptoCoin>();

            public string ErrorMessage { get; private set; } = string.Empty;

            public static FetchResult Successful(List<CryptoCoin> coins)
            {
                return new FetchResult
                {
                    Success = true,
                    Coins = coins
                };
            }

            public static FetchResult Failed(string errorMessage)
            {
                return new FetchResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }
    }
}
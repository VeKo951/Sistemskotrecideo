using Akka.Actor;
using projekat3.Actors;
using projekat3.Services;
using System;
using System.Threading.Tasks;

namespace projekat3
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger logger = new Logger();

            logger.Log("Aplikacija se pokrece.");

            ActorSystem actorSystem = ActorSystem.Create("CryptoTrendAnalyzerSystem");

            IActorRef cryptoActor = actorSystem.ActorOf(
                Props.Create(() => new CryptoTrendActor(logger)),
                "cryptoTrendActor"
            );

            CoinGeckoService coinGeckoService = new CoinGeckoService(logger);

            RxCryptoProducer rxCryptoProducer = new RxCryptoProducer(
                coinGeckoService,
                cryptoActor,
                logger,
                TimeSpan.FromSeconds(60)
            );

            WebServer webServer = new WebServer(
                port: 5050,
                cryptoActor: cryptoActor,
                logger: logger
            );

            try
            {
                rxCryptoProducer.Start();

                webServer.Start();

                Console.WriteLine();
                Console.WriteLine("Crypto Trend Analyzer radi.");
                Console.WriteLine("Otvori u browseru:");
                Console.WriteLine("http://localhost:5050/");
                Console.WriteLine("http://localhost:5050/trending");
                Console.WriteLine("http://localhost:5050/groups");
                Console.WriteLine("http://localhost:5050/status");
                Console.WriteLine();
                Console.WriteLine("Pritisni ENTER za zaustavljanje aplikacije...");
                Console.ReadLine();
            }
            finally
            {
                logger.Log("Aplikacija se zaustavlja.");

                rxCryptoProducer.Stop();

                await webServer.StopAsync();

                await actorSystem.Terminate();

                logger.Log("Aplikacija je zaustavljena.");
            }
        }
    }
}
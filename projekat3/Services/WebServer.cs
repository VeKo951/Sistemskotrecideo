using Akka.Actor;
using projekat3.Messages;
using projekat3.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace projekat3.Services
{
    internal class WebServer
    {
        private readonly HttpListener listener;
        private readonly IActorRef cryptoActor;
        private readonly Logger logger;
        private readonly int port;

        private bool running = false;
        private Task? listenerTask;

        public WebServer(int port, IActorRef cryptoActor, Logger logger)
        {
            this.port = port;
            this.cryptoActor = cryptoActor;
            this.logger = logger;

            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:" + port + "/");
        }

        public void Start()
        {
            running = true;

            listener.Start();

            // Ne koristimo Task.Run za async metodu.
            // ListenLoopAsync je vec async metoda i sama vraca Task.
            listenerTask = ListenLoopAsync();

            logger.Log("WebServer: pokrenut na adresi http://localhost:" + port + "/");
            logger.Log("WebServer: dostupne rute: /, /trending, /groups, /status");
        }

        private async Task ListenLoopAsync()
        {
            logger.Log("WebServer: listener petlja je pokrenuta.");

            while (running)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();

                    // Svaki zahtev se prosledjuje na obradu kao poseban async tok.
                    // Web server ne ceka da se jedan zahtev zavrsi da bi mogao da primi sledeci.
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException)
                {
                    if (running)
                    {
                        logger.Log("WebServer: HttpListenerException u listener petlji.");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        logger.Log("WebServer: neocekivana greska u listener petlji: " + ex.Message);
                    }
                }
            }

            logger.Log("WebServer: listener petlja je zaustavljena.");
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath.ToLower() ?? "/";
            string method = context.Request.HttpMethod;

            logger.Log("WebServer: primljen zahtev " + method + " " + path);

            try
            {
                if (method != "GET")
                {
                    await SendTextResponseAsync(context, 405, "Dozvoljeni su samo HTTP GET zahtevi.");
                    logger.Log("WebServer: zahtev odbijen jer nije GET.");
                    return;
                }

                if (path == "/")
                {
                    await HandleHomeAsync(context);
                    return;
                }

                if (path == "/trending")
                {
                    await HandleTrendingAsync(context);
                    return;
                }

                if (path == "/groups")
                {
                    await HandleGroupsAsync(context);
                    return;
                }

                if (path == "/status")
                {
                    await HandleStatusAsync(context);
                    return;
                }

                await SendTextResponseAsync(context, 404, "Ruta nije pronadjena.");
                logger.Log("WebServer: ruta nije pronadjena: " + path);
            }
            catch (Exception ex)
            {
                logger.Log("WebServer: greska pri obradi zahteva " + path + ": " + ex.Message);

                try
                {
                    await SendTextResponseAsync(context, 500, "Doslo je do greske na serveru: " + ex.Message);
                }
                catch
                {
                    // Ako ni slanje greske ne uspe, samo ignorisemo da server ne pukne.
                }
            }
        }

        private async Task HandleHomeAsync(HttpListenerContext context)
        {
            string body = @"
                <h1>Crypto Trend Analyzer</h1>
                <p>Aplikacija koristi Rx.NET, Akka.NET i CoinGecko API za analizu trendujucih kriptovaluta.</p>

                <ul>
                    <li><a href=""/trending"">/trending</a> - lista trendujucih kriptovaluta</li>
                    <li><a href=""/groups"">/groups</a> - grupisanje po promeni cene za 24h</li>
                    <li><a href=""/status"">/status</a> - status aplikacije</li>
                </ul>

                <p><b>Napomena:</b> Web server ne poziva CoinGecko API direktno. On salje poruke Akka.NET aktoru.</p>
            ";

            await SendHtmlResponseAsync(context, 200, BuildPage("Crypto Trend Analyzer", body));

            logger.Log("WebServer: pocetna strana uspesno poslata.");
        }

        private async Task HandleTrendingAsync(HttpListenerContext context)
        {
            logger.Log("WebServer: salje GetTrendingMessage aktoru.");

            List<CryptoCoin> coins = await cryptoActor.Ask<List<CryptoCoin>>(
                new GetTrendingMessage(),
                TimeSpan.FromSeconds(5)
            );

            string body = "<h1>Trendujuce kriptovalute</h1>";

            if (coins.Count == 0)
            {
                body += "<p>Podaci jos nisu dostupni. Sacekajte prvo Rx azuriranje.</p>";
            }
            else
            {
                body += BuildCoinsTable(coins);
            }

            body += "<p><a href=\"/\">Nazad</a></p>";

            await SendHtmlResponseAsync(context, 200, BuildPage("Trendujuce kriptovalute", body));

            logger.Log("WebServer: /trending zahtev uspesno obradjen. Broj kriptovaluta: " + coins.Count);
        }

        private async Task HandleGroupsAsync(HttpListenerContext context)
        {
            logger.Log("WebServer: salje GetGroupedMessage aktoru.");

            Dictionary<string, List<CryptoCoin>> groups =
                await cryptoActor.Ask<Dictionary<string, List<CryptoCoin>>>(
                    new GetGroupedMessage(),
                    TimeSpan.FromSeconds(5)
                );

            string body = "<h1>Kriptovalute grupisane po promeni za 24h</h1>";

            if (groups.Count == 0)
            {
                body += "<p>Podaci jos nisu dostupni. Sacekajte prvo Rx azuriranje.</p>";
            }
            else
            {
                foreach (var group in groups)
                {
                    body += "<h2>" + WebUtility.HtmlEncode(group.Key) + "</h2>";
                    body += BuildCoinsTable(group.Value);
                }
            }

            body += "<p><a href=\"/\">Nazad</a></p>";

            await SendHtmlResponseAsync(context, 200, BuildPage("Grupisanje kriptovaluta", body));

            logger.Log("WebServer: /groups zahtev uspesno obradjen. Broj grupa: " + groups.Count);
        }

        private async Task HandleStatusAsync(HttpListenerContext context)
        {
            logger.Log("WebServer: salje GetStatusMessage aktoru.");

            CryptoStatusMessage status = await cryptoActor.Ask<CryptoStatusMessage>(
                new GetStatusMessage(),
                TimeSpan.FromSeconds(5)
            );

            string lastUpdatedText = status.LastUpdated == DateTime.MinValue
                ? "Podaci jos nisu azurirani"
                : status.LastUpdated.ToString("dd.MM.yyyy. HH:mm:ss");

            string body = @"
                <h1>Status aplikacije</h1>
                <table>
                    <tr><th>Poslednje azuriranje</th><td>" + WebUtility.HtmlEncode(lastUpdatedText) + @"</td></tr>
                    <tr><th>Broj kriptovaluta</th><td>" + status.CoinCount + @"</td></tr>
                    <tr><th>Uspesna azuriranja</th><td>" + status.SuccessfulUpdates + @"</td></tr>
                    <tr><th>Neuspesna azuriranja</th><td>" + status.FailedUpdates + @"</td></tr>
                    <tr><th>Poslednja greska</th><td>" + WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(status.LastError) ? "Nema greske" : status.LastError) + @"</td></tr>
                </table>
                <p><a href=""/"">Nazad</a></p>
            ";

            await SendHtmlResponseAsync(context, 200, BuildPage("Status aplikacije", body));

            logger.Log("WebServer: /status zahtev uspesno obradjen.");
        }

        private string BuildCoinsTable(List<CryptoCoin> coins)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<table>");
            sb.Append("<tr>");
            sb.Append("<th>Naziv</th>");
            sb.Append("<th>Simbol</th>");
            sb.Append("<th>Cena USD</th>");
            sb.Append("<th>Promena 24h</th>");
            sb.Append("<th>Grupa</th>");
            sb.Append("</tr>");

            foreach (CryptoCoin coin in coins)
            {
                sb.Append("<tr>");
                sb.Append("<td>" + WebUtility.HtmlEncode(coin.Name) + "</td>");
                sb.Append("<td>" + WebUtility.HtmlEncode(coin.Symbol) + "</td>");
                sb.Append("<td>$" + FormatPrice(coin.CurrentPrice) + "</td>");
                sb.Append("<td>" + FormatChange(coin.PriceChangePercentage24h) + "%</td>");
                sb.Append("<td>" + WebUtility.HtmlEncode(coin.Group) + "</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            return sb.ToString();
        }

        private string FormatPrice(decimal price)
        {
            return price.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private string FormatChange(double change)
        {
            return change.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private string BuildPage(string title, string body)
        {
            return @"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""utf-8"">
                    <title>" + WebUtility.HtmlEncode(title) + @"</title>
                    <style>
                        body {
                            font-family: Arial, sans-serif;
                            margin: 40px;
                            background-color: #f5f5f5;
                        }

                        h1 {
                            color: #222;
                        }

                        table {
                            border-collapse: collapse;
                            width: 100%;
                            background-color: white;
                            margin-bottom: 30px;
                        }

                        th, td {
                            border: 1px solid #ccc;
                            padding: 8px;
                            text-align: left;
                        }

                        th {
                            background-color: #222;
                            color: white;
                        }

                        a {
                            color: #0066cc;
                            font-weight: bold;
                        }
                    </style>
                </head>
                <body>
                    " + body + @"
                </body>
                </html>
            ";
        }

        private async Task SendHtmlResponseAsync(HttpListenerContext context, int statusCode, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);

            context.Response.OutputStream.Close();
        }

        private async Task SendTextResponseAsync(HttpListenerContext context, int statusCode, string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);

            context.Response.OutputStream.Close();
        }

        public async Task StopAsync()
        {
            running = false;

            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
                // Ignorisemo greske pri zaustavljanju listener-a.
            }

            if (listenerTask != null)
            {
                try
                {
                    await listenerTask;
                }
                catch
                {
                    // Ignorisemo greske pri zaustavljanju listener taska.
                }
            }

            logger.Log("WebServer: zaustavljen.");
        }
    }
}
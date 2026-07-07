namespace projekat3.Models
{
    internal class CryptoCoin
    {
        public string Id { get; set; } = string.Empty;

        public string Symbol { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public decimal CurrentPrice { get; set; }

        public double PriceChangePercentage24h { get; set; }

        public string Group { get; set; } = string.Empty;
    }
}
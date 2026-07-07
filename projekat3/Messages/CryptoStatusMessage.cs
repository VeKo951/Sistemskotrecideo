using System;

namespace projekat3.Messages
{
    internal class CryptoStatusMessage
    {
        public DateTime LastUpdated { get; set; }

        public int CoinCount { get; set; }

        public int SuccessfulUpdates { get; set; }

        public int FailedUpdates { get; set; }

        public string LastError { get; set; } = string.Empty;
    }
}
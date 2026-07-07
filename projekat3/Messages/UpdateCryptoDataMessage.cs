using System.Collections.Generic;
using projekat3.Models;

namespace projekat3.Messages
{
    internal class UpdateCryptoDataMessage
    {
        public List<CryptoCoin> Coins { get; }

        public UpdateCryptoDataMessage(List<CryptoCoin> coins)
        {
            Coins = coins;
        }
    }
}
using System;

namespace projekat3.Messages
{
    internal class UpdateFailedMessage
    {
        public string ErrorMessage { get; }
        public DateTime ErrorTime { get; }

        public UpdateFailedMessage(string errorMessage)
        {
            ErrorMessage = errorMessage;
            ErrorTime = DateTime.Now;
        }
    }
}
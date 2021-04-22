using System;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers
{
    public record Lsn
    {
        private const int Base16 = 16;

        public Lsn(string lsn)
        {
            if (lsn == null)
            {
                throw new ArgumentNullException(nameof(lsn));
            }

            if (string.IsNullOrWhiteSpace(lsn))
            {
                throw new ArgumentException($"Invalid lsn string {lsn}");
            }

            StringValue = lsn;

            var split = lsn.Split(":");

            if (split.Length != 3)
            {
                throw new ArgumentException($"Lsn string should consist of three parts separated by \":\". Instead received: {lsn}");
            }

            VlfSequenceNumber = Convert.ToInt64(split[0], Base16);
            LogBlockOffset = Convert.ToInt64(split[1], Base16);
            LogBlockSlotNumber = Convert.ToInt64(split[2], Base16);
        }

        public Lsn(long vlfSequenceNumber, long logBlockOffset, long logBlockSlotNumber)
        {
            VlfSequenceNumber = vlfSequenceNumber;
            LogBlockOffset = logBlockOffset;
            LogBlockSlotNumber = logBlockSlotNumber;

            // this does not include zero padding on the left, so don't compare it as a string.
            StringValue = $"{Convert.ToString(VlfSequenceNumber, Base16)}:{Convert.ToString(LogBlockOffset, Base16)}:{Convert.ToString(LogBlockSlotNumber, Base16)}";
        }

        public string StringValue { get; }
        public long VlfSequenceNumber { get; }
        public long LogBlockOffset { get; }
        public long LogBlockSlotNumber { get; }
    }
}
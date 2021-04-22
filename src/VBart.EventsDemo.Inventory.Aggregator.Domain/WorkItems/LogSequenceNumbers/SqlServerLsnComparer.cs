using System;
using System.Collections.Generic;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers
{
    public class SqlServerLsnComparer : IComparer<Lsn>, System.Collections.IComparer
    {
        public int Compare(Lsn? x, Lsn? y)
        {
            if (x == null || y == null)
            {
                throw new InvalidOperationException("Null value comparison not supported.");
            }

            var vlfComparison = x.VlfSequenceNumber.CompareTo(y.VlfSequenceNumber);
            var offsetComparison = x.LogBlockOffset.CompareTo(y.LogBlockOffset);
            var slotComparison = x.LogBlockSlotNumber.CompareTo(y.LogBlockSlotNumber);

            if (vlfComparison == 0)
            {
                return offsetComparison == 0 ? slotComparison : offsetComparison;
            }

            return vlfComparison;
        }

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null)
            {
                throw new InvalidOperationException("Null value comparison not supported.");
            }

            if (x is Lsn a && y is Lsn b)
            {
                return Compare(a, b);
            }

            throw new ArgumentException("Unexpected parameter types");
        }
    }
}

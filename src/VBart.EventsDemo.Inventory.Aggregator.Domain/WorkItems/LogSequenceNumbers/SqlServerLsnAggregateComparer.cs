using System;
using System.Collections.Generic;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers
{
    public class SqlServerLsnAggregateComparer : IComparer<LsnAggregate>, System.Collections.IComparer
    {
        private readonly SqlServerLsnComparer _lsnComparer = new();

        public int Compare(LsnAggregate? x, LsnAggregate? y)
        {
            if (x == null || y == null)
            {
                throw new InvalidOperationException("Null value comparison not supported.");
            }

            var commitLsnComparison = _lsnComparer.Compare(x.CommitLsn, y.CommitLsn);
            return commitLsnComparison == 0 ? _lsnComparer.Compare(x.ChangeLsn, y.ChangeLsn) : commitLsnComparison;
        }

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null)
            {
                throw new InvalidOperationException("Null value comparison not supported.");
            }

            if (x is LsnAggregate a && y is LsnAggregate b)
            {
                return Compare(a, b);
            }

            throw new ArgumentException("Unexpected parameter types");
        }
    }
}

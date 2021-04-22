namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers
{
    public record LsnAggregate
    {
        public LsnAggregate(string commitLsn, string changeLsn)
        {
            ChangeLsn = new Lsn(changeLsn);
            CommitLsn = new Lsn(commitLsn);
        }

        public LsnAggregate(Lsn commitLsn, Lsn changeLsn)
        {
            ChangeLsn = changeLsn;
            CommitLsn = commitLsn;
        }

        public Lsn CommitLsn { get; }

        public Lsn ChangeLsn { get; }
    }
}
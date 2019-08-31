namespace Disruptor
{
    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
    ///  using the given WaitStrategy.
    /// </summary>
    /// <typeparam name="TSequencer">the type of the <see cref="ISequencer"/> used.</typeparam>
    /// <typeparam name="TWaitStrategy">the type of the <see cref="IWaitStrategy"/> used.</typeparam>
    internal sealed class ProcessingSequenceBarrier<TSequencer, TWaitStrategy> : ISequenceBarrier
        where TWaitStrategy : IWaitStrategy
        where TSequencer : ISequencer
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TWaitStrategy _waitStrategy;
        private TSequencer _sequencer;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private readonly ISequence _dependentSequence;
        private readonly Sequence _cursorSequence;
        private readonly ActivatableSequenceBarrierAlert _alert;

        public ProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _cursorSequence = cursorSequence;
            _dependentSequence = 0 == dependentSequences.Length ? (ISequence)cursorSequence : new FixedSequenceGroup(dependentSequences);
            _alert = new ActivatableSequenceBarrierAlert(this);
        }

        public long Cursor => _dependentSequence.Value;
        public bool IsAlerted => _alert.IsActive;

        public long WaitFor(long sequence)
        {
            _alert.Check();

            var availableSequence = _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequence, _alert);

            if (availableSequence < sequence)
                return availableSequence;

            return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
        }

        public void Alert()
        {
            _alert.Activate();
            _waitStrategy.SignalAllWhenBlocking();
        }

        public void ClearAlert()
        {
            _alert.Deactivate();
        }
    }
}

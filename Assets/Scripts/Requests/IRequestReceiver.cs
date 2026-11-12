namespace ResortEmpire.Requests
{
    public interface IRequestReceiver
    {
        // Reserve a place before returning true. False (or an exception) must leave the request unaccepted.
        // Later, report completion through RequestQueues.TryCompleteRequest before announcing the free slot.
        bool TryAccept(QueuedRequest request);
    }
}

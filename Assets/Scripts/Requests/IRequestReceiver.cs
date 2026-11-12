namespace ResortEmpire.Requests
{
    public interface IRequestReceiver
    {
        // Reserve a place before returning true. False (or an exception) must leave the request unaccepted.
        bool TryAccept(QueuedRequest request);
    }
}

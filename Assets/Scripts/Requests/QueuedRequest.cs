namespace ResortEmpire.Requests
{
    public readonly struct QueuedRequest
    {
        public RequestArrival Request { get; }

        public double WaitingSinceSeconds { get; }


        internal QueuedRequest(RequestArrival request, double waitingSinceSeconds)
        {
            Request = request;
            WaitingSinceSeconds = waitingSinceSeconds;
        }
    }
}

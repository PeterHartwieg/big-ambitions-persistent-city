namespace PersistentCity.Core
{
    public sealed class PublishedRoute
    {
        private Route? route;

        public Route? Route
        {
            get => route;
            set => route = value != null && value.IsCommitted ? TransportComposition.Copy(value) : value;
        }
    }
}

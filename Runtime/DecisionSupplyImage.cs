namespace PersistentCity.Runtime
{
    public sealed class DecisionSupplyImage<T>
    {
        public DecisionSupplyImage(T value) { Value = value; }
        public T Value { get; private set; }
        public void ObserveLive(T value) => Value = value;
    }
}

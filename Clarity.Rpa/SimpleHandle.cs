namespace Clarity.Rpa
{
    public class SimpleHandle<T>
    {
        public T Value { get; set; }

        public SimpleHandle()
        {
        }

        public SimpleHandle(T initialValue)
        {
            this.Value = initialValue;
        }
    }
}

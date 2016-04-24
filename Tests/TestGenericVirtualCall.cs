namespace Tests
{
    public class TestGenericVirtualCall
    {
        public interface ISimpleReturn
        {
            T PassThrough<T>(T v);
        }

        public class MyClass : ISimpleReturn
        {
            public T PassThrough<T>(T v)
            {
                return v;
            }
        }

        public class ConstrainedCaller<T> where T : ISimpleReturn
        {
            public V PassThrough<V>(T p, V s)
            {
                return p.PassThrough<V>(s);
            }
        }

        public void Run()
        {
            ConstrainedCaller<MyClass> constrainedCaller = new ConstrainedCaller<MyClass>();
            MyClass myClass = new MyClass();

            myClass.PassThrough<string>("OK");
            TestApi.WriteLine(constrainedCaller.PassThrough<string>(myClass, "OK"));
        }
    }
}

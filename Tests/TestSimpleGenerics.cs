using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestSimpleGenerics
    {
        public interface IMyInterface
        {
            int Operate();
        }

        public class MyGeneric<T>
            where T : IMyInterface
        {
            public T ReturnT(T v)
            {
                return v;
            }

            public int OperateOnT(T v)
            {
                return v.Operate();
            }

            public bool CallEquals(T va, T vb)
            {
                return va.Equals(vb);
            }

            public int CallGetHashCode(T v)
            {
                return v.GetHashCode();
            }

            public Type CallGetType(T v)
            {
                return v.GetType();
            }

            public string CallToString(T v)
            {
                return v.ToString();
            }
        }

        public struct MyStruct : IMyInterface
        {
            public int m_v;

            public MyStruct(int v)
            {
                m_v = v;
            }

            int IMyInterface.Operate()
            {
                return m_v;
            }
        }

        public struct MyClass : IMyInterface
        {
            public int m_v;

            public MyClass(int v)
            {
                m_v = v;
            }

            int IMyInterface.Operate()
            {
                return m_v;
            }
        }

        public void Run()
        {
            MyClass c = new MyClass(1);
            MyStruct s = new MyStruct(2);

            MyGeneric<MyClass> gc = new MyGeneric<MyClass>();
            MyGeneric<MyStruct> gs = new MyGeneric<MyStruct>();

            MyClass c2 = gc.ReturnT(c);
            MyStruct s2 = gs.ReturnT(s);

            c.m_v = 3;
            s.m_v = 4;

            TestApi.WriteLine(c2.m_v.ToString());
            TestApi.WriteLine(s2.m_v.ToString());

            TestApi.WriteLine(gs.CallEquals(s2, s).ToString());
            TestApi.WriteLine(gs.CallGetHashCode(s).ToString());
            TestApi.WriteLine(gs.CallGetType(s).ToString());
            TestApi.WriteLine(gs.CallToString(s));
            TestApi.WriteLine(gs.OperateOnT(s).ToString());
        }
    }
}

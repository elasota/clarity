using System;
using System.Collections.Generic;

namespace Tests
{
    public class TestGenericOverrides
    {
        public interface MyBaseInterface<T1, T2>
        {
            void Collidable(T1 a, T2 b);
        }

        public abstract class MyGeneric<T1, T2>
        {
            public class NestedChild<T3> : MyBaseInterface<T1, T2>
            {
                public virtual void Collidable(T2 a, T1 b)
                {
                    TestApi.WriteLine("Collidable T2 T1 (MyGeneric)");
                }

                public virtual void Collidable(T1 a, T2 b)
                {
                    TestApi.WriteLine("Collidable T1 T2 (MyGeneric)");
                }

                public virtual void Collidable(int a, int b)
                {
                    TestApi.WriteLine("Collidable int int (MyGeneric)");
                }

                public virtual void Overridable(char a, T1 b)
                {
                    TestApi.WriteLine("Overridable char T1 (MyGeneric)");
                }

                public void TryCollidable21(T2 a, T1 b)
                {
                    Collidable(a, b);
                }

                public void TryOverridable(T2 a, T1 b)
                {
                    Collidable(a, b);
                }
            }
        }

        public class MyFinalClass : MyGeneric<int, int>.NestedChild<int>
        {
            public sealed override void Overridable(char a, int b)
            {
                TestApi.WriteLine("Overridable char int (MyFinalClass)");
            }

            static void StaticImpl()
            {
                TestApi.WriteLine("Static impl");
            }
        }

        public class MyConflicting<T>
        {
            public virtual void Func(T a)
            {
                TestApi.WriteLine("Wrong");
            }

            public virtual void Func(int a)
            {
                TestApi.WriteLine("Wrong");
            }
        }

        public class MyConflictResolving : MyConflicting<int>
        {
            public new virtual void Func(int a)
            {
                TestApi.WriteLine("Right");
            }
        }

        public void Run()
        {
            MyFinalClass c = new MyFinalClass();
            MyBaseInterface<int, int> i = (MyBaseInterface<int, int>)c;
            i.Collidable(0, 0);
            c.TryCollidable21(0, 0);

            MyConflictResolving cr = new MyConflictResolving();
            cr.Func(0);
        }
    }
}

namespace Clarity.Rpa
{
    public class ArrayCloner
    {
        public static T[] Clone<T>(T[] arr)
        {
            long len = arr.LongLength;
            T[] newArray = new T[len];
            for (long i = 0; i < len; i++)
                newArray[i] = arr[i];
            return newArray;
        }
    }
}

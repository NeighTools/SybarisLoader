using System.Collections;

namespace SybarisLoader.Util
{
    /// <summary>
    ///     A helper wrapper for a generic stack.
    /// </summary>
    /// <remarks>
    ///     Made for use in <see cref="SimpleJSON" /> without the need of importing anything.
    /// </remarks>
    /// <typeparam name="T">Type of the objec to contain.</typeparam>
    public class Stack<T> : Stack where T : class
    {
        public bool Contains(T obj) => base.Contains(obj);

        public new T Peek() => base.Peek() as T;

        public new T Pop() => base.Pop() as T;

        public void Push(T obj)
        {
            base.Push(obj);
        }
    }
}
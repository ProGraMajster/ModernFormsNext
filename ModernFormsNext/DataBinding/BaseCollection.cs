using System;
using System.Collections;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides a minimal base implementation for data-binding collections.
    /// </summary>
    /// <remarks>
    ///  Derived collections expose their storage through <see cref="List"/>. The collection is not
    ///  synchronized and should be accessed from the UI thread that owns the binding components.
    /// </remarks>
    public abstract class BaseCollection : ICollection
    {
        /// <summary>
        ///  Gets the number of elements in the collection.
        /// </summary>
        public virtual int Count => List.Count;

        /// <summary>
        ///  Gets a value indicating whether access to the collection is synchronized.
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        ///  Gets an object that can be used to synchronize access to the collection.
        /// </summary>
        public object SyncRoot => List.SyncRoot;

        /// <summary>
        ///  Gets the list used by the derived collection.
        /// </summary>
        protected abstract ArrayList List { get; }

        /// <summary>
        ///  Copies the elements of the collection to an array, starting at the specified index.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> where copying begins.</param>
        public void CopyTo(Array array, int index)
        {
            List.CopyTo(array, index);
        }

        /// <summary>
        ///  Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        public IEnumerator GetEnumerator()
        {
            return List.GetEnumerator();
        }
    }
}

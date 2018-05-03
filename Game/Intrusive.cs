using System;
using System.Collections;
using System.Collections.Generic;

namespace Primevil.Game
{   
    // Objects that want to be in intrusive lists embed links of this type
    // (it is a struct, so it will be embedded in the object, and not incur allocation overhead)
    public struct IntrusiveLink<TLinkKey>
    {
        public delegate void Handler(ref IntrusiveLink<TLinkKey> link);

        internal IIntrusiveMember<TLinkKey> Prev;
        internal IIntrusiveMember<TLinkKey> Next;

        public void Unlink()
        {
            if (Prev == null)
                return; // we allow Unlink to be called regardless of whether it is currently linked
            var prev = Prev;
            var next = Next;
            prev.WithLink((ref IntrusiveLink<TLinkKey> prevLink) => {
                prevLink.Next = next;
            });
            next.WithLink((ref IntrusiveLink<TLinkKey> nextLink) => {
                nextLink.Prev = prev;
            });
            Prev = null;
            Next = null;
        }
    }


    /* 
     * An object that wishes to be member of an intrusive list must implement this interface,
     * in order to give the list implementation access to its link, with its prev and next pointers.
     * We index the interface with a phantom type in order to allow an object to be part of
     * multiple list, by implementing this interface multiple times with different phantom types.
     */
    public interface IIntrusiveMember<TLinkKey>
    {
        void WithLink(IntrusiveLink<TLinkKey>.Handler func);
    }


    public class IntrusiveList<TValue, TLinkKey> : IIntrusiveMember<TLinkKey>, IEnumerable<TValue> where TValue : class, IIntrusiveMember<TLinkKey>
    {
        // embed a link struct in the list object
        private IntrusiveLink<TLinkKey> _head;
        public void WithLink(IntrusiveLink<TLinkKey>.Handler func)
        {
            func(ref _head);
        }


        public IntrusiveList()
        {
            // we implement a circular list, so both Prev and Next are both equal to this initially
            _head = new IntrusiveLink<TLinkKey> { Prev = this, Next = this };
        }

        public void PushBack(TValue x)
        {
            var a = _head.Prev;
            var b = this;
            _head.Prev = x;
            x.WithLink((ref IntrusiveLink<TLinkKey> xlink) => {
                if (xlink.Prev != null || xlink.Next != null)
                    throw new InvalidOperationException("adding list link that is already linked");
                xlink.Prev = a;
                xlink.Next = b;
            });
            a.WithLink((ref IntrusiveLink<TLinkKey> alink) => {
                alink.Next = x;
            });
        }

        public void PushFront(TValue x)
        {
            var a = this;
            var b = _head.Next;
            _head.Next = x;
            x.WithLink((ref IntrusiveLink<TLinkKey> xlink) => {
                if (xlink.Prev != null || xlink.Next != null)
                    throw new InvalidOperationException("adding list link that is already linked");
                xlink.Prev = a;
                xlink.Next = b;
            });
            b.WithLink((ref IntrusiveLink<TLinkKey> blink) => {
                blink.Prev = x;
            });
        }

        public void PopBack()
        {
            if (Empty)
                throw new InvalidOperationException("popping from empty list");
            _head.Prev.WithLink((ref IntrusiveLink<TLinkKey> link) => link.Unlink());
        }

        public void PopFront()
        {
            if (Empty)
                throw new InvalidOperationException("popping from empty list");
            _head.Next.WithLink((ref IntrusiveLink<TLinkKey> link) => link.Unlink());
        }

        public TValue Back
        {
            get
            {
                if (Empty)
                    throw new InvalidOperationException("list is empty");
                return (TValue)_head.Prev;
            }
        }

        public TValue Front
        {
            get
            {
                if (Empty)
                    throw new InvalidOperationException("list is empty");
                return (TValue)_head.Next;
            }
        }

        public void Clear()
        {
            while (!Empty)
                PopFront();
        }

        public bool Empty
        {
            get { return ReferenceEquals(this, _head.Next); }
        }


        public IEnumerator<TValue> GetEnumerator()
        {
            var x = _head.Next;
            while (!ReferenceEquals(x, this)) {
                yield return (TValue)x;
                x.WithLink((ref IntrusiveLink<TLinkKey> link) => {
                    x = link.Next;
                });
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }



    /*public class IntrusiveHashTable<TKey, TValue, TLinkKey> where TValue : class, IIntrusiveMember<TLinkKey>
    {
        private IntrusiveList<TValue, TLinkKey>[] _buckets;

        public IntrusiveHashTable(int initialBucketCount)
        {

        }
    }*/
}

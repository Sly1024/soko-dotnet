using System;
using System.Collections;
using System.Collections.Generic;

namespace soko
{
    public class MoveRanges : DynamicList<Move>
    {
        private DynamicList<int> firstFree = new DynamicList<int>(10);
        private DynamicList<Move> addRangeList = new DynamicList<Move>(10);

        public MoveRanges(int capacity) : base(capacity)
        {
            idx = 2;    // skip index 0... and 1, because...
        }

        public void StartAddRange()
        {
            addRangeList.idx = 0;
        }

        public void AddRangeItem(Move item)
        {
            addRangeList.Add(item);
        }

        public int FinishAddRange()
        {
            var count = addRangeList.idx;
            if (count == 0) return -1;

            int toIdx = GetFreeSlot(count);

            EnsureCapacity(toIdx + count + 1);
            Array.Copy(addRangeList.items, 0, items, toIdx, count);
            if (idx == toIdx) idx += (count == 1 ? 2 : count); // make sure we allocate at least 2 items

            items[toIdx+count-1].SetLastBit();
            return toIdx;
        }

        private int GetFreeSlot(int size)
        {
            var freeIdx = firstFree[size];
            if (freeIdx == 0) return idx;
            firstFree[size] = items[freeIdx].encoded | (items[freeIdx+1].encoded << 16);
            return freeIdx;
        }

        private void RemoveRange(int start, int end)
        {
            var size = end - start;
            if (size == 1) size = 2;
            items[start].encoded = (ushort)firstFree[size];
            items[start+1].encoded = (ushort)(firstFree[size] >> 16);
            firstFree[size] = start;
        }

        public IEnumerable<Move> GetRangeAt(int idx) 
        {
            return new RangeEnumerator(this, idx);
        }

        private class RangeEnumerator : IEnumerable<Move>, IEnumerator<Move>
        {
            MoveRanges ranges;
            int startIdx;
            int currIdx;
            bool wasLast = false;

            public RangeEnumerator(MoveRanges ranges, int startIdx)
            {
                this.ranges = ranges;
                this.startIdx = startIdx;
                currIdx = startIdx-1;
            }

            public Move Current => ranges.items[currIdx];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (wasLast) {
                    ranges.RemoveRange(startIdx, ++currIdx);
                    return false;
                }

                ++currIdx;
                wasLast = Current.IsLast;
                return true;
            }

            public void Reset()
            {
                currIdx = startIdx-1;
                wasLast = false;
            }

            public IEnumerator<Move> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyImporter.TCLR
{
    public sealed class BinaryBlobRepository
    {
        public struct Item
        {
            public BinaryBlob Blob { get; private set; }
            public uint TableOffset { get; private set; }

            public Item(BinaryBlob blob, uint tableOffset)
            {
                this = new Item();
                Blob = blob;
                TableOffset = tableOffset;
            }

            public void Relocate(uint loc)
            {
                TableOffset = loc;
            }
        }

        private List<Item> m_items;
        private Dictionary<BinaryBlob, int> m_cache;
        private uint m_size;

        public BinaryBlobRepository()
        {
            m_items = new List<Item>();
            m_cache = new Dictionary<BinaryBlob, int>();
            m_size = 1;
        }

        public uint Index(BinaryBlob blob)
        {
            int index;
            if (m_cache.TryGetValue(blob, out index))
                return m_items[index].TableOffset;
            uint offset = m_size;
            m_size += (uint)blob.Bytes.Length;
            m_cache[blob] = m_items.Count;
            m_items.Add(new Item(blob, offset));
            return offset;
        }

        public void WriteAll(BinaryWriter writer)
        {
            writer.Write((byte)0);
            foreach (Item item in m_items)
                writer.Write(item.Blob.Bytes);
        }
    }
}

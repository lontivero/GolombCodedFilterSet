﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GolombCodeFilterSet;
using NBitcoin;

namespace MagicalCryptoWallet.Backend
{

	public interface IKeyValueRandomAccessStore<TKey, TItem>
	{
		TItem GetFrom(int offset);
		int Put(TKey key, TItem item);
	}

	public interface IKeyValueStore<TKey, TItem>
	{
		TItem Get(TKey key);
		void Put(TKey key, TItem item);
	}

	public class GcsFilterRepository : IKeyValueStore<uint256, GcsFilter>
	{
		private readonly IKeyValueRandomAccessStore<uint256, GcsFilter> _store;
		private readonly IKeyValueStore<uint256, int> _index;


		public GcsFilterRepository(
			IKeyValueRandomAccessStore<uint256, GcsFilter> store, 
			IKeyValueStore<uint256, int> index)
		{
			_store = store;
			_index = index;
		}

		public GcsFilter Get(uint256 key)
		{
			var offset = _index.Get(key);
			return _store.GetFrom(offset);
		}

		public void Put(uint256 key, GcsFilter filter)
		{
			var pos = _store.Put(key, filter);
			_index.Put(key, pos);
		}
	}

	public abstract class Store<T> : IEnumerable<T>
	{
		private readonly Stream _stream;

		protected Store(Stream stream)
		{
			_stream = stream;
		}

		public int Put(T item)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			using (var bw = new BinaryWriter(_stream, new UTF8Encoding(false, false), true))
			{
				var pos = _stream.Position;
				Write(bw, item);
				return (int)pos;
			}
		}

		protected IEnumerable<T> Enumerate(int offset = 0)
		{
			_stream.Seek(offset, SeekOrigin.Begin);
			using (var br = new BinaryReader(_stream, new UTF8Encoding(false, false), true))
			{
				while (_stream.Position < _stream.Length)
				{
					yield return Read(br);
				}
			}
		}

		protected abstract T Read(BinaryReader reader);

		protected abstract void Write(BinaryWriter writer, T item);

		public IEnumerator<T> GetEnumerator()
		{
			return Enumerate().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public class GcsFilterStore : Store<GcsFilter>, IKeyValueRandomAccessStore<uint256, GcsFilter>
	{
		private const short MagicSeparatorNumber = 0x4691;

		public GcsFilterStore(Stream stream) 
			: base(stream)
		{
		}

		protected override GcsFilter Read(BinaryReader reader)
		{
			var magic = reader.ReadInt16();
			if (magic != MagicSeparatorNumber)
				return null;
			var entryCount = reader.ReadInt32();
			var bitArrayLen = reader.ReadInt32();
			var byteArrayLen = FastBitArray.GetArrayLength(bitArrayLen, 8);
			var data = reader.ReadBytes(byteArrayLen);
			var bitArray = new FastBitArray(data);
			bitArray.Length = bitArrayLen;
			return new GcsFilter (bitArray, 20, entryCount);
		}

		protected override void Write(BinaryWriter writer, GcsFilter filter)
		{
			var data = filter.Data.ToByteArray();

			writer.Write(MagicSeparatorNumber);
			writer.Write(filter.N);
			writer.Write(filter.Data.Length);
			writer.Write(data);
			writer.Flush();
		}

		public GcsFilter GetFrom(int offset)
		{
			return Enumerate(offset).First();
		}

		public int Put(uint256 key, GcsFilter filter)
		{
			return Put(filter);
		}
	}


	public class GcsFilterIndex : Store<GcsFilterIndexEntry>, IKeyValueStore<uint256, int>
	{
		public GcsFilterIndex(Stream stream)
			: base(stream)
		{
		}

		public virtual int Get(uint256 key)
		{
			return this.Single(x=>x.Key == key).Offset;
		}

		public void Put(uint256 key, int offset)
		{
			Put(new GcsFilterIndexEntry(key, offset));
		}

		protected override GcsFilterIndexEntry Read(BinaryReader reader)
		{
			var key = reader.ReadBytes(32);
			var pos = reader.ReadInt32();
			return new GcsFilterIndexEntry(new uint256(key), pos);
		}

		protected override void Write(BinaryWriter writer, GcsFilterIndexEntry indexEntry)
		{
			writer.Write(indexEntry.Key.ToBytes(), 0, 32);
			writer.Write(indexEntry.Offset);
		}
	}

	public class PreloadedFilterIndex : IKeyValueStore<uint256, int>
	{
		private readonly GcsFilterIndex _index;
		private readonly Dictionary<uint256, int> _cache;

		public PreloadedFilterIndex(GcsFilterIndex index)
		{
			_index = index;
			_cache = new Dictionary<uint256, int>();
			Load();
		}

		private void Load()
		{
			foreach (var i in _index)
			{
				_cache.Add(i.Key, i.Offset);
			}
		}

		public int Get(uint256 key)
		{
			return _cache[key];
		}

		public void Put(uint256 key, int offset)
		{
			_index.Put(key, offset);
			_cache[key] = offset;
		}
	}


	public class GcsFilterIndexEntry
	{
		public uint256 Key { get; }
		public int Offset { get; }

		public GcsFilterIndexEntry(uint256 key, int offset)
		{
			Key = key;
			Offset = offset;
		}
	}
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GolombCodeFilterSet
{
	public class IndexedFilter
	{
		public BitArray Data { get; }
		public List<(ulong, int)> Index { get; }

		public IndexedFilter(BitArray data, List<(ulong, int)> index)
		{
			Data = data;
			Index = index;
		}
	}


	/// <summary>
	/// Implements a Golomb-coded set to be use in the creation of client-side filter
	/// for a new kind Bitcoin light clients. This code is based on the BIP:
	/// https://github.com/Roasbeef/bips/blob/master/gcs_light_client.mediawiki
	/// </summary>
	public class Filter
	{
		public byte P { get; internal set; }
		public int N { get; internal set; }
		public ulong ModulusP { get; internal set; }
		public ulong ModulusNP { get; internal set; }
		public IndexedFilter IndexedFilter { get; internal set; }
		private BitArray _bitArray;
		
		public static Filter Build(byte[] k, byte P, IEnumerable<byte[]> data)
		{
			// NOTE: P must be a power of two as we target the specialized case of Golomb coding: Golomb - Rice coding.
			if (P == 0x00 || (P & (P - 1)) != 0)
				throw new ArgumentException("P has to be a power of two value", nameof(P));

			var bytesData = data as byte[][] ?? data.ToArray();
			if (data == null || !bytesData.Any())
				throw new ArgumentException("data can not be null or empty array", nameof(data));

			var modP = 1UL << P;

			var hs = ConstructHashedSet(P, k, bytesData);
			var filterData = Compress(hs, P);
			var N = bytesData.Length;

			return new Filter
			{
				P = P,
				N = N,
				ModulusP = modP,
				ModulusNP = ((ulong)N) * modP,
				IndexedFilter = filterData
			};
		}

		private static List<ulong> ConstructHashedSet(byte P, byte[] key, IEnumerable<byte[]> data)
		{
			// N the number of items to be inserted into the set.
			var dataArrayBytes = data as byte[][] ?? data.ToArray();
			var N = dataArrayBytes.Count();

			// The list of data item hashes.
			var values = new ConcurrentBag<ulong>();
			var modP = 1UL << P;
			var modNP = ((ulong)N) * modP;
			var nphi = modNP >> 32;
			var nplo = (ulong)((uint)modNP);

			// Process the data items and calculate the 64 bits hash for each of them.
			Parallel.ForEach(dataArrayBytes, item =>
			{
				var hash = SipHasher.Hash(key, item);
				var value = FastReduction(hash, nphi, nplo);
				values.Add(value);
			});

			var ret = values.ToList();
			ret.Sort();
			return ret;
		}

		private static IndexedFilter Compress(List<ulong> values, byte P)
		{
			var indexCount = (values.Count / 128) + 1;
			var filterIndex = new List<(ulong, int)>(indexCount);
			var bitArray = new BitArray();
			var bitStream = new BitStream(bitArray);
			var sw = new GRCodedStreamWriter(bitStream, P);

			var i = 0;
			var pos = 0;
			var lastValue = 0UL;
			foreach (var value in values)
			{
				if(i % indexCount == 0) 
					filterIndex.Add((lastValue, pos));
				pos = sw.Write(value);
				lastValue = value;
				i++;
			}
			return new IndexedFilter(bitArray, filterIndex);
		}


		public bool Match(byte[] data, byte[] key)
		{
			var nphi = ModulusNP >> 32;
			var nplo = ((ulong)((uint)ModulusNP));

			var hash = SipHasher.Hash(key, data);
			var searchValue = FastReduction(hash, nphi, nplo);

			var lastPos = 0;
			var pos = 0;
			var lastValue = 0UL;
			var value = 0UL;
			foreach (var index in IndexedFilter.Index)
			{
				lastPos = pos;
				lastValue = value;
				if (index.Item1 > searchValue)
					break;

				pos = index.Item2;
				value = index.Item1;
			}

			var bitStream = new BitStream(IndexedFilter.Data);
			bitStream.Position = lastPos;
			var sr = new GRCodedStreamReader(bitStream, P, lastValue);
			if (lastValue == searchValue)
				return true;
			try
			{
				var currentValue = sr.Read();
				while (currentValue < searchValue)
				{
					currentValue = sr.Read();
				}
				if (currentValue == searchValue)
					return true;
			}
			catch (ArgumentOutOfRangeException)
			// This means we reached the end of the bits stream ao, the value was not found.
			{
				return false;
			}
			return false;
		}

		public bool MatchAny(IEnumerable<byte[]> data, byte[] key)
		{
			if (data == null || !data.Any())
				throw new ArgumentException("data can not be null or empty array", nameof(data));

			var hs = ConstructHashedSet(P, key, data);

			var lastValue1 = 0UL;
			var lastValue2 = hs[0];
			var i = 1;

			var bitStream = new BitStream(IndexedFilter.Data);
			var sr = new GRCodedStreamReader(bitStream, P, 0);

			while (lastValue1 != lastValue2)
			{
				if (lastValue1 > lastValue2)
				{
					if (i < hs.Count)
					{
						lastValue2 = hs[i];
						i++;
					}
					else
					{
						return false;
					}
				}
				else if (lastValue2 > lastValue1)
				{
					var val = sr.Read();
					lastValue1 = val;
				}
			}
			return true;
		}

		private static ulong FastReduction(ulong value, ulong nhi, ulong nlo)
		{
			// First, we'll spit the item we need to reduce into its higher and lower bits.
			var vhi = value >> 32;
			var vlo = (ulong)((uint)value);

			// Then, we distribute multiplication over each part.
			var vnphi = vhi * nhi;
			var vnpmid = vhi * nlo;
			var npvmid = nhi * vlo;
			var vnplo = vlo * nlo;

			// We calculate the carry bit.
			var carry = ((ulong)((uint)vnpmid) + (ulong)((uint)npvmid) +
			(vnplo >> 32)) >> 32;

			// Last, we add the high bits, the middle bits, and the carry.
			value = vnphi + (vnpmid >> 32) + (npvmid >> 32) + carry;

			return value;
		}

	}
}
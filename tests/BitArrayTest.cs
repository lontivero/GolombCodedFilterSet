using System;
using System.Collections.Generic;
using GolombCodeFilterSet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GolombCodedFilterSet.UnitTests
{
	[TestClass]
	public class FastBitArrayTest
	{
		[TestMethod]
		public void GetBitsTest()
		{
			// 1 1 1 0 1 0 1 1 - 1 0 1 0 1 0 1 1 - 1 0 1 0 1 1 1 0 - 1 0 1 0 1 1 1 0 
			// 1 0 1 1 1 0 1 0 
			var barr = new BitArray();
			barr.Length = 50;
			for (int i = 0; i < 40; i++)
			{
				if (i % 7 == 0)
				{
					barr[i] = true;
					i++;
					barr[i] = true;
				}
				else
				{
					barr[i] = i % 2 == 0;
				}
			}

			// Get bits in the same int.
			Assert.AreEqual((ulong) 0b111, barr.GetBits(0, 3));
			Assert.AreEqual((ulong) 0b10111, barr.GetBits(0, 5));
			Assert.AreEqual((ulong) 0b01010111010, barr.GetBits(3, 11));

			// Get bits in cross int.
			Assert.AreEqual((ulong) 0b101110101110101, barr.GetBits(24, 16));
		}

		[TestMethod]
		public void SetRandomBitsTest()
		{
			var barr = new BitArray(new byte[0]);
			barr.Length = 150;
			var values = new List<int>();
			var lengths = new List<int>();
			var rnd = new Random();
			var pos = 0;

			for (int i = 0; i < 10; i++)
			{
				var val = rnd.Next();
				var len = rnd.Next(1, 20);
				barr.SetBits(pos, (ulong) val, len);

				values.Add(val);
				lengths.Add(len);
				pos += len;
			}

			pos = 0;
			for (int i = 0; i < 10; i++)
			{
				var len = lengths[i];
				var expectedValue = values[i];
				var value = barr.GetBits(pos, len);
				Assert.AreEqual(((ulong) expectedValue & value), value);
				pos += len;
			}
		}

		[TestMethod]
		public void SetBitAndGetBitsTest()
		{
			var barr = new BitArray(new byte[0]);
			barr.Length = 150;

			var j = true;
			for (int i = 0; i < 64; i += 2)
			{
				if (j)
				{
					barr.SetBit(i, true);
					barr.SetBit(i + 1, true);
				}
				else
				{
					barr.SetBit(i, false);
					barr.SetBit(i + 1, false);
				}

				j = !j;
			}

			for (int i = 0; i < 16; i++)
			{
				Assert.AreEqual(0b11UL, barr.GetBits(i * 4, 2));
				Assert.AreEqual(0b00UL, barr.GetBits((i * 4) + 2, 2));
			}

			for (int i = 0; i < 8; i++)
			{
				Assert.AreEqual(0b0011UL, barr.GetBits(i * 8, 4));
				Assert.AreEqual(0b0011UL, barr.GetBits((i * 8) + 4, 2));
			}

			Assert.AreEqual(0b11001UL, barr.GetBits(29, 5));
		}

		public void SetBitsBigEndianTest()
		{
			var barr = new BitArray(new byte[0]);
			barr.Length = 5;
			barr.SetBits(0, 14, 4);
			var val = barr.GetBits(0, 4);

			barr = new BitArray(new byte[0]);
			barr.Length = 5;
			barr.SetBit(0, false);
			barr.SetBit(1, true);
			barr.SetBit(2, true);
			barr.SetBit(3, true);
			val = barr.GetBits(0, 4);
		}
	}
}

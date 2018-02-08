using System;
using GolombCodeFilterSet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;


namespace GolombCodedFilterSet.UnitTests
{
	[TestClass]
	public class PerformanceTest
	{
		[TestMethod]
		public void SimulationTest()
		{
			// Given this library can be used for building and query filters for each block of 
			// the bitcoin's blockchain, we must be sure it performs well, specially in the queries.

			// Considering a 4MB block (overestimated) with an average transaction size of 250 bytes (underestimated)
			// gives us 16000 transactions (this is about 27 tx/sec). Assuming 2.5 txouts per tx we have 83885 txouts 
			// per block.
			const int blockCount = 100;
			const int maxBlockSize = 4 * 1000 * 1000;
			const int avgTxSize = 250;					// currently the average is around 1kb
			const int txoutCountPerBlock = maxBlockSize / avgTxSize;
			const int avgTxoutPushDataSize = 20;		// P2PKH scripts has 20 bytes
			const int walletAddressCount = 1000;        // We estimate that our user will have 1000 addresses.

			var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

			// Generation of data to be added into the filter
			var random = new Random();

			var blocks= new List< (Filter, List<byte[]>)>(blockCount);
			for (var i=0; i<blockCount;i++)
			{
				var txouts = new List<byte[]>(txoutCountPerBlock);
				for (var j = 0; j < txoutCountPerBlock; j++)
				{
					var pushDataBuffer = new byte[avgTxoutPushDataSize];
					random.NextBytes(pushDataBuffer);
					txouts.Add(pushDataBuffer);
				}

				var filter = Filter.Build(key, 16, txouts);
				blocks.Add((filter,txouts));
			}


			// Check that the filter can match every single txout in every block
			foreach (var block in blocks)
			{
				var filter = block.Item1;
				var txouts = block.Item2;
				foreach (var txout in txouts)
				{
					Assert.IsTrue(filter.Match(txout, key));
				}
			}

			var walletAddresses = new List<byte[]>(walletAddressCount);
			var falsePositiveCount = 0;
			for (int i = 0; i < walletAddressCount; i++)
			{
				var walletAddress = new byte[avgTxoutPushDataSize];
				random.NextBytes(walletAddress);
				walletAddresses.Add(walletAddress);
			}

			// Check that the filter can match every single txout in every block
			foreach (var block in blocks)
			{
				var filter = block.Item1;
				var txouts = block.Item2;
				if (filter.MatchAny(walletAddresses, key))
					falsePositiveCount++;
			}

			Assert.IsTrue(falsePositiveCount < 100);
		}
	}
}

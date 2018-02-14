﻿using System;
using GolombCodeFilterSet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MagicalCryptoWallet.Backend;
using NBitcoin.Crypto;


namespace GolombCodedFilterSet.UnitTests
{
	[TestClass]
	public class PersistenceTest
	{
		[TestMethod]
		public void CreateStore()
		{
			const byte P = 20;
			const int blockCount = 100;
			const int maxBlockSize = 4 * 1000 * 1000;
			const int avgTxSize = 250; // Currently the average is around 1kb.
			const int txoutCountPerBlock = maxBlockSize / avgTxSize;
			const int avgTxoutPushDataSize = 20; // P2PKH scripts has 20 bytes.

			var key = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15};

			// Generation of data to be added into the filter
			var random = new Random();
			var dataDirectory = new DirectoryInfo("./data");
			if (dataDirectory.Exists)
			{
				foreach (var fileInfo in dataDirectory.GetFiles())
				{
					fileInfo.Delete();
				}
			}

			var blocks = new List<GcsFilter>(blockCount);
			using (var filterStream = DirectoryStream.Open(dataDirectory, "filter-????.dat"))
			{
				using (var indexStream = DirectoryStream.Open(dataDirectory, "index-????.dat"))
				{
					var filterStore = new GcsFilterStore(filterStream);
					var indexStore = new GcsFilterIndex(indexStream);
					var fastIndexStore = new PreloadedFilterIndex(indexStore);
					var repo = new GcsFilterRepository(filterStore, fastIndexStore);

					for (var i = 0; i < blockCount; i++)
					{
						var txouts = new List<byte[]>(txoutCountPerBlock);
						for (var j = 0; j < txoutCountPerBlock; j++)
						{
							var pushDataBuffer = new byte[avgTxoutPushDataSize];
							random.NextBytes(pushDataBuffer);
							txouts.Add(pushDataBuffer);
						}

						var filter = GcsFilter.Build(key, P, txouts);
						blocks.Add(filter);
						repo.Put(Hashes.Hash256(filter.Data.ToByteArray()), filter);
					}
				}
			}

			using (var filterStream = DirectoryStream.Open(dataDirectory, "filter-????.dat"))
			{
				using (var indexStream = DirectoryStream.Open(dataDirectory, "index-????.dat"))
				{
					var filterStore = new GcsFilterStore(filterStream);
					var indexStore = new GcsFilterIndex(indexStream);
					var fastIndexStore = new PreloadedFilterIndex(indexStore);
					var repo = new GcsFilterRepository(filterStore, fastIndexStore);

					var blockIndexes = Enumerable.Range(0, blockCount).ToList();
					blockIndexes.Shuffle();

					foreach (var blkIndx in blockIndexes)
					{
						var block = blocks[blkIndx];
						var blockFilter = block;
						var blockFilterId = Hashes.Hash256(blockFilter.Data.ToByteArray());
						var savedFilter = repo.Get(blockFilterId);
						var savedFilterId = Hashes.Hash256(savedFilter.Data.ToByteArray());
						Assert.AreEqual(blockFilterId, savedFilterId);
					}
				}
			}
		}
	}
}
